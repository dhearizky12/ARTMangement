using System.Text.Json;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using BantuBantu.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
namespace BantuBantu.Tests;

/// <summary>Transactional notification tests. A fake IEmailSender stands in
/// for Resend; nothing here touches the network. State is built directly in
/// a dedicated PostgreSQL test database; every test deletes the rows it
/// created.</summary>
public sealed class NotificationTests
{
    // Dedicated database: notification tests migrate and seed heavily, so they
    // must not share a physical database with the 2FA/auth tests (concurrent
    // migrates and shared AdminAccounts would flake both ways). CI creates it
    // in the postgres service job; locally create bantubantu_notify_test.
    private static string Connection =>
        Environment.GetEnvironmentVariable("BANTUBANTU_NOTIFICATION_TEST_DB")
        ?? Environment.GetEnvironmentVariable("BANTUBANTU_2FA_TEST_DB")
        ?? Environment.GetEnvironmentVariable("BANTUBANTU_TEST_DB")
        ?? throw new InvalidOperationException("Set BANTUBANTU_NOTIFICATION_TEST_DB to a dedicated EMPTY PostgreSQL test database.");
    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DatabaseConnectionStringResolver.Convert(Connection)).Options);
        db.Database.Migrate();
        return db;
    }
    private sealed class RecordingSender : IEmailSender
    {
        private readonly object gate = new();
        public readonly List<EmailMessage> Sent = [];
        public Func<EmailMessage, Exception?> FailWith = _ => null;
        public TimeSpan? DelayFor;
        public Func<EmailMessage, bool> DelayWhen = _ => true;
        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (DelayFor.HasValue && DelayWhen(message)) await Task.Delay(DelayFor.Value, ct);
            var failure = FailWith(message);
            if (failure is not null) throw failure;
            lock (gate) Sent.Add(message);
        }
    }
    private sealed class StaticActor(Actor actor) : ICurrentActor
    {
        public Actor Get() => actor;
    }
    private sealed class FakeWilayah(VillageResult? village) : IWilayahRepository
    {
        public Task<IReadOnlyList<VillageResult>> SearchAsync(string query, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VillageResult>>([]);
        public Task<VillageResult?> FindAsync(string id, CancellationToken ct) => Task.FromResult(village);
    }
    private sealed record Harness(AppDbContext Db, RecordingSender Sender, NotificationService Service, NotificationSettings Settings, NotificationRenderer Renderer, TwoFactorSettings TwoFactor)
    {
        public async Task SaveAsync() => await Db.SaveChangesAsync();
        public void Dispose() => Db.Dispose();
    }
    private static Harness NewHarness(bool enabled = true, bool twoFactorEnabled = false, int maxPerHour = 100, int maxAttempts = 6, int opportunisticTimeoutSeconds = 30)
    {
        var db = NewDb();
        var sender = new RecordingSender();
        var settings = new NotificationSettings { Enabled = enabled, OutboxPollSeconds = 30, MaxAttempts = maxAttempts, MaxPerRecipientPerHour = maxPerHour, OpportunisticTimeoutSeconds = opportunisticTimeoutSeconds };
        var renderer = new NotificationRenderer(new AppSettings { FrontendBaseUrl = "https://app.example.test" }, new EmailSettings());
        var twoFactor = new TwoFactorSettings { Enabled = twoFactorEnabled };
        var outbox = new NotificationOutbox(db, settings, renderer, sender, NullLogger<NotificationOutbox>.Instance);
        var service = new NotificationService(outbox, new NotificationData(db), settings, twoFactor, NullLogger<NotificationService>.Instance);
        return new Harness(db, sender, service, settings, renderer, twoFactor);
    }
    private static string[] SentTo(Harness harness, string tag) =>
        harness.Sender.Sent.Where(m => m.To.StartsWith(tag + "-")).Select(m => m.To).OrderBy(x => x).ToArray();
    private static string[] SentSubjects(Harness harness, string tag) =>
        harness.Sender.Sent.Where(m => m.To.StartsWith(tag + "-")).Select(m => m.Subject).OrderBy(x => x).ToArray();
    private static IServiceProvider WorkerServices(AppDbContext db, RecordingSender sender, NotificationRenderer renderer) =>
        new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailSender>(sender)
            .AddSingleton<INotificationRenderer>(renderer)
            .AddLogging()
            .BuildServiceProvider();

    // Fixed synthetic ids (10/6/4/2 chars) that cannot collide with seeded data.
    private const string ProvinceId = "99";
    private const string RegencyId = "9999";
    private const string DistrictId = "999999";
    private const string VillageId = "9999999999";

    private static async Task SeedAreaAsync(AppDbContext db)
    {
        if (!await db.Provinces.AnyAsync(p => p.Id == ProvinceId))
        {
            db.Provinces.Add(new Province { Id = ProvinceId, Name = "Provinsi Uji" });
            db.Regencies.Add(new Regency { Id = RegencyId, Name = "Kabupaten Uji", ProvinceId = ProvinceId });
            db.Districts.Add(new District { Id = DistrictId, Name = "Kecamatan Uji", RegencyId = RegencyId });
            db.Villages.Add(new Village { Id = VillageId, Name = "Kelurahan Uji", DistrictId = DistrictId, Type = VillageType.Kelurahan });
            await db.SaveChangesAsync();
        }
    }
    private static async Task<Guid> SeedCategoryAsync(AppDbContext db)
    {
        var existing = await db.ServiceCategories.SingleOrDefaultAsync(c => c.Slug == "uji-kategori");
        if (existing is not null) return existing.Id;
        var category = new ServiceCategory { Slug = "uji-kategori", Name = "Kategori Uji", Description = "d", IconKey = "x", SortOrder = 99, IsActive = true };
        db.ServiceCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }
    private static async Task<(Guid CustomerId, Guid ProviderId, Guid OrderId)> SeedOrderGraphAsync(AppDbContext db, string tag, bool agencyScoped, bool providerVerified)
    {
        var customer = new User { Email = $"{tag}-customer@example.test", FullName = "Budi Santoso" };
        db.Users.Add(customer);
        Guid? agencyId = null;
        if (agencyScoped)
        {
            var agency = new Agency { Name = $"{tag} Agency", ContactInfo = "c", Status = AgencyStatus.Approved };
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
            agencyId = agency.Id;
            db.AdminAccounts.Add(new AdminAccount { Email = $"{tag}-agency-admin@example.test", FullName = "Admin Agency", Role = UserRole.AgencyAdmin, AgencyId = agency.Id });
        }
        db.AdminAccounts.Add(new AdminAccount { Email = $"{tag}-platform-admin@example.test", FullName = "Admin Platform", Role = UserRole.PlatformAdmin });
        var provider = new Provider
        {
            AgencyId = agencyId,
            FullName = "Sari Wulandari",
            ApplicationStatus = ProviderApplicationStatus.Approved,
            VerificationStatus = VerificationStatus.Verified
        };
        provider.Credential = new ProviderCredential { ProviderId = provider.Id, Provider = provider, Email = $"{tag}-provider@example.test", PasswordHash = "x", EmailVerifiedAt = providerVerified ? DateTimeOffset.UtcNow : null };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();
        var order = new Order
        {
            CustomerId = customer.Id,
            ProviderId = provider.Id,
            Status = OrderStatus.Pending,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Price = 100000,
            PricingType = PricingType.PerVisit,
            VillageId = VillageId,
            AddressDetail = "Jl. Mawar No. 999, RT 01 RW 02"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (customer.Id, provider.Id, order.Id);
    }
    private static async Task DeleteGraphAsync(AppDbContext db, string tag)
    {
        var reviews = await db.Reviews.Where(r => r.Customer.Email.StartsWith(tag + "-")).ToListAsync();
        db.Reviews.RemoveRange(reviews);
        var orders = await db.Orders.Where(o => o.Customer.Email.StartsWith(tag + "-")).ToListAsync();
        db.Orders.RemoveRange(orders);
        var providers = await db.Providers.Where(p => p.Credential != null && p.Credential.Email.StartsWith(tag + "-")).ToListAsync();
        db.Providers.RemoveRange(providers);
        var admins = await db.AdminAccounts.Where(a => a.Email.StartsWith(tag + "-")).ToListAsync();
        db.AdminAccounts.RemoveRange(admins);
        var users = await db.Users.Where(u => u.Email.StartsWith(tag + "-")).ToListAsync();
        db.Users.RemoveRange(users);
        var agencies = await db.Agencies.Where(a => a.Name.StartsWith(tag + " ")).ToListAsync();
        db.Agencies.RemoveRange(agencies);
        var outbox = await db.EmailOutboxes.Where(o => o.To.StartsWith(tag + "-") || o.To == "customer@example.test").ToListAsync();
        db.EmailOutboxes.RemoveRange(outbox);
        var templates = new[] { "order.", "application.", "provider.", "agency.", "review.", "password." };
        var stray = await db.EmailOutboxes.Where(o => templates.Any(t => o.TemplateKey.StartsWith(t))).ToListAsync();
        db.EmailOutboxes.RemoveRange(stray);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task OrderBookedReachesProviderCustomerAndAgencyAdmin()
    {
        const string tag = "nb-booked-agency";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (customerId, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: true, providerVerified: true);
            await harness.Service.EmitOrderBookedAsync(Guid.NewGuid(), customerId, providerId, VillageId, "12 Januari 2026", default);
            await harness.SaveAsync();
            await harness.Service.DispatchEnqueuedAsync(default);
            await harness.SaveAsync();
            Assert.Equal([$"{tag}-agency-admin@example.test", $"{tag}-customer@example.test", $"{tag}-provider@example.test"], SentTo(harness, tag));
            var providerMail = harness.Sender.Sent.Single(m => m.To == $"{tag}-provider@example.test");
            Assert.StartsWith("[Bantu-Bantu] Pesanan baru dari Budi", providerMail.Subject);
            Assert.Contains("https://app.example.test/provider/dashboard?tab=pesanan", providerMail.Html);
            Assert.DoesNotContain("Jl. Mawar", providerMail.Html);
            Assert.DoesNotContain("Jl. Mawar", providerMail.Text);
            Assert.Contains("Kelurahan Uji", providerMail.Html);
            Assert.Contains("Budi", providerMail.Html);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task OrderBookedForDirectProviderReachesPlatformAdmin()
    {
        const string tag = "nb-booked-direct";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (customerId, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            await harness.Service.EmitOrderBookedAsync(Guid.NewGuid(), customerId, providerId, VillageId, "12 Januari 2026", default);
            await harness.SaveAsync();
            await harness.Service.DispatchEnqueuedAsync(default);
            await harness.SaveAsync();
            Assert.Equal([$"{tag}-customer@example.test", $"{tag}-platform-admin@example.test", $"{tag}-provider@example.test"], SentTo(harness, tag));
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task OrderStatusChangeReachesProviderAndCustomer_ButNotWhenUnchanged()
    {
        const string tag = "nb-status";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, orderId) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            var version = (await harness.Db.Orders.SingleAsync(o => o.Id == orderId)).Version;
            await harness.Service.EmitOrderStatusAsync(orderId, version, "Pending", "Pending", default);
            await harness.SaveAsync();
            Assert.Empty(await harness.Db.EmailOutboxes.Where(o => o.DedupeKey.Contains(orderId.ToString())).ToListAsync());
            await harness.Service.EmitOrderStatusAsync(orderId, version, "Pending", "Confirmed", default);
            await harness.SaveAsync();
            await harness.Service.DispatchEnqueuedAsync(default);
            await harness.SaveAsync();
            Assert.Equal([$"{tag}-customer@example.test", $"{tag}-provider@example.test"], SentTo(harness, tag));
            var customerMail = harness.Sender.Sent.Single(m => m.To == $"{tag}-customer@example.test");
            Assert.Equal("[Bantu-Bantu] Pesanan dikonfirmasi", customerMail.Subject);
            Assert.Contains("https://app.example.test/orders", customerMail.Html);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task DuplicateEmitIsDeduped()
    {
        const string tag = "nb-dedupe";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, orderId) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            var version = (await harness.Db.Orders.SingleAsync(o => o.Id == orderId)).Version;
            CancellationToken ct = default;
            await harness.Service.EmitOrderStatusAsync(orderId, version, "Pending", "Confirmed", ct);
            await harness.Service.EmitOrderStatusAsync(orderId, version, "Pending", "Confirmed", ct);
            await harness.SaveAsync();
            Assert.Equal(2, await harness.Db.EmailOutboxes.CountAsync(o => o.To.StartsWith(tag + "-")));
            await harness.Service.DispatchEnqueuedAsync(ct);
            await harness.SaveAsync();
            Assert.Equal(2, harness.Sender.Sent.Count);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task RollbackSendsNothing()
    {
        const string tag = "nb-rollback";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (customerId, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            // Stage rows but never commit: disposing without SaveChanges must persist nothing.
            await harness.Service.EmitOrderBookedAsync(Guid.NewGuid(), customerId, providerId, VillageId, "12 Januari 2026", default);
            harness.Dispose();
            Assert.Empty(harness.Sender.Sent);
            using var fresh = NewDb();
            try
            {
                Assert.Empty(await fresh.EmailOutboxes.Where(o => o.To.StartsWith(tag + "-")).ToListAsync());
            }
            finally { fresh.Dispose(); }
            // And a rejected transition (409) emits nothing at the service level.
            var harness2 = NewHarness();
            try
            {
                var repo = new MarketplaceRepository(harness2.Db, new ProviderScope());
                var orders = new OrderService(repo, new StaticActor(new Actor(Guid.NewGuid(), UserRole.PlatformAdmin, null)), new FakeWilayah(null), harness2.Service);
                var orderId = (await harness2.Db.Orders.SingleAsync(o => o.Customer.Email == $"{tag}-customer@example.test")).Id;
                var ex = await Assert.ThrowsAsync<ProfileException>(() => orders.ChangeOrder(orderId, new OrderStatusRequest(OrderStatus.Completed), default));
                Assert.Equal(409, ex.Status);
                Assert.Empty(await harness2.Db.EmailOutboxes.Where(o => o.To.StartsWith(tag + "-")).ToListAsync());
                Assert.Empty(harness2.Sender.Sent);
            }
            finally { await DeleteGraphAsync(harness2.Db, tag); harness2.Dispose(); }
        }
        finally { using var cleanup = NewDb(); try { await DeleteGraphAsync(cleanup, tag); } finally { cleanup.Dispose(); } }
    }

    [Fact]
    public async Task RetryThenSuccess()
    {
        const string tag = "nb-retry";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            CancellationToken ct = default;
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.SaveAsync();
            var failures = 0;
            // Only the provider row fails; the admin row sends immediately.
            harness.Sender.FailWith = m => m.To == $"{tag}-provider@example.test" && failures++ < 2 ? new TransientEmailException("boom") : null;
            var services = WorkerServices(harness.Db, harness.Sender, harness.Renderer);
            await EmailOutboxWorker.ProcessBatchAsync(services, harness.Settings, ct);
            var row = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-provider@example.test");
            Assert.Equal(EmailOutboxStatus.Pending, row.Status);
            Assert.Equal(1, row.Attempts);
            row.NextAttemptAt = DateTimeOffset.UtcNow;
            await harness.SaveAsync();
            await EmailOutboxWorker.ProcessBatchAsync(services, harness.Settings, ct);
            row = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-provider@example.test");
            Assert.Equal(EmailOutboxStatus.Pending, row.Status);
            Assert.Equal(2, row.Attempts);
            row.NextAttemptAt = DateTimeOffset.UtcNow;
            await harness.SaveAsync();
            await EmailOutboxWorker.ProcessBatchAsync(services, harness.Settings, ct);
            row = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-provider@example.test");
            Assert.Equal(EmailOutboxStatus.Sent, row.Status);
            Assert.NotNull(row.SentAt);
            Assert.Single(harness.Sender.Sent, m => m.To == $"{tag}-provider@example.test");
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task PermanentFailureIsNotRetried()
    {
        const string tag = "nb-perm";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            CancellationToken ct = default;
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.SaveAsync();
            var calls = 0;
            harness.Sender.FailWith = m => { if (m.To != $"{tag}-provider@example.test") return null; calls++; return new PermanentEmailException("HTTP 403 sandbox"); };
            var services = WorkerServices(harness.Db, harness.Sender, harness.Renderer);
            await EmailOutboxWorker.ProcessBatchAsync(services, harness.Settings, ct);
            var row = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-provider@example.test");
            Assert.Equal(EmailOutboxStatus.Failed, row.Status);
            Assert.Equal(0, row.Attempts);
            Assert.Contains("sandbox", row.LastError);
            await EmailOutboxWorker.ProcessBatchAsync(services, harness.Settings, ct);
            Assert.Equal(1, calls);
            Assert.DoesNotContain(harness.Sender.Sent, m => m.To == $"{tag}-provider@example.test");
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task HourlyCapSuppressesBeyondLimit()
    {
        const string tag = "nb-cap";
        var harness = NewHarness(maxPerHour: 1);
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            CancellationToken ct = default;
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.Service.EmitProviderReactivatedAsync(providerId, 1, ct);
            await harness.SaveAsync();
            var rows = await harness.Db.EmailOutboxes.Where(o => o.To == $"{tag}-provider@example.test").OrderBy(o => o.CreatedAt).ToListAsync();
            Assert.Equal(2, rows.Count);
            // Different events have separate caps; same event twice hits the cap.
            await harness.Service.EmitProviderReactivatedAsync(providerId, 2, ct);
            await harness.SaveAsync();
            rows = await harness.Db.EmailOutboxes.Where(o => o.To == $"{tag}-provider@example.test").OrderBy(o => o.CreatedAt).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.Equal(EmailOutboxStatus.Suppressed, rows[2].Status);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task OpportunisticSendRespectsBudgetAndLeavesRowsPending()
    {
        const string tag = "nb-budget";
        var harness = NewHarness(opportunisticTimeoutSeconds: 1);
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            var ct = default(CancellationToken);
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.SaveAsync();
            harness.Sender.DelayFor = TimeSpan.FromSeconds(5);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await harness.Service.DispatchEnqueuedAsync(ct);
            sw.Stop();
            await harness.SaveAsync();
            Assert.Empty(harness.Sender.Sent);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4), $"dispatch took {sw.Elapsed}, budget is 1s");
            var rows = await harness.Db.EmailOutboxes.Where(o => o.To.StartsWith(tag + "-")).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.Equal(EmailOutboxStatus.Pending, r.Status);
                Assert.Equal(0, r.Attempts);
                Assert.True(r.NextAttemptAt <= DateTimeOffset.UtcNow.AddMinutes(1));
            });
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task OpportunisticSendMixedFastAndSlow()
    {
        const string tag = "nb-mixed";
        var harness = NewHarness(opportunisticTimeoutSeconds: 1);
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            var ct = default(CancellationToken);
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.SaveAsync();
            harness.Sender.DelayFor = TimeSpan.FromSeconds(5);
            harness.Sender.DelayWhen = m => m.To == $"{tag}-provider@example.test";
            await harness.Service.DispatchEnqueuedAsync(ct);
            await harness.SaveAsync();
            Assert.Single(harness.Sender.Sent, m => m.To == $"{tag}-platform-admin@example.test");
            var providerRow = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-provider@example.test");
            Assert.Equal(EmailOutboxStatus.Pending, providerRow.Status);
            Assert.Equal(0, providerRow.Attempts);
            var adminRow = await harness.Db.EmailOutboxes.SingleAsync(o => o.To == $"{tag}-platform-admin@example.test");
            Assert.Equal(EmailOutboxStatus.Sent, adminRow.Status);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task HtmlEscapesInjectedMarkup()
    {
        const string tag = "nb-escape";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            var provider = await harness.Db.Providers.SingleAsync(p => p.Id == providerId);
            provider.FullName = "<script>alert(1)</script>";
            await harness.SaveAsync();
            CancellationToken ct = default;
            await harness.Service.EmitProviderReactivatedAsync(providerId, provider.Version, ct);
            await harness.SaveAsync();
            await harness.Service.DispatchEnqueuedAsync(ct);
            await harness.SaveAsync();
            var mail = Assert.Single(harness.Sender.Sent);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", mail.Html);
            Assert.DoesNotContain("<script>alert", mail.Html);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task UnverifiedProviderSkippedWhen2faOn_SentWhen2faOff()
    {
        const string tag = "nb-gate";
        var gated = NewHarness(twoFactorEnabled: true);
        try
        {
            await SeedAreaAsync(gated.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(gated.Db, tag, agencyScoped: false, providerVerified: false);
            await gated.Service.EmitApplicationSubmittedAsync(providerId, 0, default);
            await gated.SaveAsync();
            await gated.Service.DispatchEnqueuedAsync(default);
            await gated.SaveAsync();
            Assert.DoesNotContain(gated.Sender.Sent, m => m.To == $"{tag}-provider@example.test");
            Assert.Empty(await gated.Db.EmailOutboxes.Where(o => o.To == $"{tag}-provider@example.test").ToListAsync());
        }
        finally { await DeleteGraphAsync(gated.Db, tag); gated.Dispose(); }
        var open = NewHarness(twoFactorEnabled: false);
        try
        {
            await SeedAreaAsync(open.Db);
            var (_, providerId, _) = await SeedOrderGraphAsync(open.Db, tag, agencyScoped: false, providerVerified: false);
            await open.Service.EmitApplicationSubmittedAsync(providerId, 0, default);
            await open.SaveAsync();
            await open.Service.DispatchEnqueuedAsync(default);
            await open.SaveAsync();
            Assert.Contains(open.Sender.Sent, m => m.To == $"{tag}-provider@example.test");
        }
        finally { await DeleteGraphAsync(open.Db, tag); open.Dispose(); }
    }

    [Fact]
    public async Task ProviderWithoutEmailIsSkippedWithoutFailure()
    {
        const string tag = "nb-noemail";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var provider = new Provider { FullName = "Tanpa Email", ApplicationStatus = ProviderApplicationStatus.Draft, VerificationStatus = VerificationStatus.Pending };
            harness.Db.Providers.Add(provider);
            await harness.SaveAsync();
            await harness.Service.EmitApplicationDecisionAsync(provider.Id, provider.Version, NotificationEvents.ApplicationApproved, null, default);
            await harness.SaveAsync();
            Assert.Empty(await harness.Db.EmailOutboxes.Where(o => o.DedupeKey.Contains(provider.Id.ToString())).ToListAsync());
            Assert.Empty(harness.Sender.Sent);
            harness.Db.Providers.Remove(provider);
            await harness.SaveAsync();
        }
        finally { harness.Dispose(); }
    }

    [Fact]
    public async Task FlagOff_SendsNothingAndStoresNothing()
    {
        const string tag = "nb-off";
        var harness = NewHarness(enabled: false);
        try
        {
            await SeedAreaAsync(harness.Db);
            var (customerId, providerId, orderId) = await SeedOrderGraphAsync(harness.Db, tag, agencyScoped: false, providerVerified: true);
            CancellationToken ct = default;
            await harness.Service.EmitOrderBookedAsync(Guid.NewGuid(), customerId, providerId, VillageId, "x", ct);
            await harness.Service.EmitOrderStatusAsync(orderId, 0, "Pending", "Confirmed", ct);
            await harness.Service.EmitReviewReceivedAsync(orderId, 0, 5, ct);
            await harness.Service.EmitApplicationSubmittedAsync(providerId, 0, ct);
            await harness.Service.EmitApplicationDecisionAsync(providerId, 0, NotificationEvents.ApplicationApproved, null, ct);
            await harness.Service.EmitAgencyStatusAsync(Guid.NewGuid(), "Suspended", ct);
            await harness.Service.EmitPasswordChangedAsync(providerId, "x@y.zz", "N", "1", true, ct);
            await harness.SaveAsync();
            await harness.Service.DispatchEnqueuedAsync(ct);
            await harness.SaveAsync();
            Assert.Empty(await harness.Db.EmailOutboxes.Where(o => o.To.StartsWith(tag + "-")).ToListAsync());
            Assert.Empty(harness.Sender.Sent);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task AuthOtpStillSendsImmediately()
    {
        var sender = new RecordingSender();
        var renderer = new NotificationRenderer(new AppSettings { FrontendBaseUrl = "https://app.example.test" }, new EmailSettings());
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var directory = Path.Combine(Path.GetTempPath(), "bantubantu-otp-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "public.pem"), rsa.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(Path.Combine(directory, "private.pem"), rsa.ExportRSAPrivateKeyPem());
        var keys = new RsaKeys(new Microsoft.Extensions.Configuration.ConfigurationManager
        {
            ["Jwt:PrivateKeyPath"] = Path.Combine(directory, "private.pem"),
            ["Jwt:PublicKeyPath"] = Path.Combine(directory, "public.pem"),
            ["Jwt:KeyId"] = "test"
        });
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager { ["ASPNETCORE_ENVIRONMENT"] = "Production" };
        var otp = new EmailOtpService(keys, sender, renderer, config, NullLogger<EmailOtpService>.Instance);
        await otp.SendAsync("customer@example.test", "481516", 10, default);
        var message = Assert.Single(sender.Sent);
        Assert.Equal("Kode masuk Bantu-Bantu", message.Subject);
        Assert.Contains("481516", message.Html);
        Assert.Contains("* bantu-bantu.", message.Html);
        Assert.Contains("481516", message.Text);
        using var db = NewDb();
        try { Assert.Empty(await db.EmailOutboxes.Where(o => o.To == "customer@example.test").ToListAsync()); }
        finally { db.Dispose(); }
    }

    [Fact]
    public void RetryDelaysRoughlySpanADay()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), EmailOutboxWorker.RetryDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(30), EmailOutboxWorker.RetryDelay(2));
        Assert.Equal(TimeSpan.FromHours(1), EmailOutboxWorker.RetryDelay(3));
        Assert.Equal(TimeSpan.FromHours(8), EmailOutboxWorker.RetryDelay(6));
        Assert.Equal(TimeSpan.FromHours(12), EmailOutboxWorker.RetryDelay(99));
    }

    [Fact]
    public void ThemeMatchesFrontendTokens()
    {
        var tokens = TokenFile();
        Assert.Equal(Token(tokens, "color-primary"), EmailTheme.Primary);
        Assert.Equal(Token(tokens, "color-bg"), EmailTheme.PageBackground);
        Assert.Equal(Token(tokens, "color-surface"), EmailTheme.Surface);
        Assert.Equal(Token(tokens, "color-ink"), EmailTheme.Ink);
        Assert.Equal(Token(tokens, "color-muted"), EmailTheme.Muted);
        Assert.Equal(Token(tokens, "color-accent"), EmailTheme.Accent);
        Assert.Equal(Token(tokens, "color-accent-ink"), EmailTheme.AccentInk);
        Assert.Equal(Token(tokens, "color-danger"), EmailTheme.Danger);
        Assert.Equal(Token(tokens, "border-width"), EmailTheme.BorderWidth);
        Assert.Equal(Token(tokens, "radius"), EmailTheme.Radius);
        Assert.Equal(Token(tokens, "shadow-offset"), EmailTheme.ShadowOffset);
    }
    private static string TokenFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "frontend", "src", "styles", "tokens.css");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        throw new InvalidOperationException("frontend/src/styles/tokens.css not found above " + AppContext.BaseDirectory);
    }
    private static string Token(string css, string name)
    {
        var marker = $"--{name}:";
        var index = css.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0, $"token --{name} missing from tokens.css");
        var end = css.IndexOf(';', index);
        return css[(index + marker.Length)..end].Trim();
    }

    [Fact]
    public async Task FullOrderLifecycleThroughServices()
    {
        const string tag = "nb-lifecycle";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var categoryId = await SeedCategoryAsync(harness.Db);
            var customer = new User { Email = $"{tag}-customer@example.test", FullName = "Budi Santoso" };
            harness.Db.Users.Add(customer);
            var provider = new Provider { FullName = "Sari Wulandari", ApplicationStatus = ProviderApplicationStatus.Approved, VerificationStatus = VerificationStatus.Verified };
            provider.Credential = new ProviderCredential { ProviderId = provider.Id, Provider = provider, Email = $"{tag}-provider@example.test", PasswordHash = "x", EmailVerifiedAt = DateTimeOffset.UtcNow };
            provider.Categories.Add(new ProviderCategory { ProviderId = provider.Id, ServiceCategoryId = categoryId });
            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
                provider.Availability.Add(new ProviderAvailability { ProviderId = provider.Id, DayOfWeek = day, IsAvailable = true });
            harness.Db.Providers.Add(provider);
            harness.Db.AdminAccounts.Add(new AdminAccount { Email = $"{tag}-platform-admin@example.test", FullName = "Admin", Role = UserRole.PlatformAdmin });
            await harness.SaveAsync();
            CancellationToken ct = default;
            var repo = new MarketplaceRepository(harness.Db, new ProviderScope());
            var village = new VillageResult(VillageId, "Kelurahan Uji", "Kelurahan", "Kecamatan Uji", "Kabupaten Uji", "Provinsi Uji", "Kelurahan Uji, Kecamatan Uji");
            var orders = new OrderService(repo, new StaticActor(new Actor(customer.Id, UserRole.Customer, null)), new FakeWilayah(village), harness.Service);
            var booked = await orders.Book(new BookingRequest(provider.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), VillageId, "Jl. Mawar No. 1, RT 01"), ct);
            Assert.Equal(OrderStatus.Pending, booked.Status);
            var adminOrders = new OrderService(repo, new StaticActor(new Actor(Guid.NewGuid(), UserRole.PlatformAdmin, null)), new FakeWilayah(village), harness.Service);
            await adminOrders.ChangeOrder(booked.Id, new OrderStatusRequest(OrderStatus.Confirmed), ct);
            await adminOrders.ChangeOrder(booked.Id, new OrderStatusRequest(OrderStatus.Completed), ct);
            var customerOrders = new OrderService(repo, new StaticActor(new Actor(customer.Id, UserRole.Customer, null)), new FakeWilayah(village), harness.Service);
            await customerOrders.Review(booked.Id, new ReviewRequest(5, "Bagus sekali"), ct);
            var subjects = SentSubjects(harness, tag);
            Assert.Contains("[Bantu-Bantu] Pesanan baru dari Budi", subjects);
            Assert.Contains("[Bantu-Bantu] Pesanan Anda diterima", subjects);
            Assert.Contains("[Bantu-Bantu] Pesanan dikonfirmasi", subjects);
            Assert.Contains("[Bantu-Bantu] Pesanan selesai", subjects);
            Assert.Contains("[Bantu-Bantu] Ulasan baru bintang 5", subjects);
            var reviewMail = harness.Sender.Sent.Single(m => m.Subject.StartsWith("[Bantu-Bantu] Ulasan baru"));
            Assert.DoesNotContain("Bagus sekali", reviewMail.Html);
            Assert.DoesNotContain("Bagus sekali", reviewMail.Text);
            var customerConfirm = harness.Sender.Sent.Single(m => m.To == $"{tag}-customer@example.test" && m.Subject == "[Bantu-Bantu] Pesanan Anda diterima");
            Assert.DoesNotContain("Jl. Mawar", customerConfirm.Html);
            Assert.DoesNotContain("Jl. Mawat", customerConfirm.Text);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task ApplicationLifecycleThroughServices()
    {
        const string tag = "nb-appcycle";
        var harness = NewHarness();
        try
        {
            await SeedAreaAsync(harness.Db);
            var categoryId = await SeedCategoryAsync(harness.Db);
            var agency = new Agency { Name = $"{tag} Agency", ContactInfo = "c", Status = AgencyStatus.Approved };
            harness.Db.Agencies.Add(agency);
            harness.Db.AdminAccounts.Add(new AdminAccount { Email = $"{tag}-agency-admin@example.test", FullName = "AA", Role = UserRole.AgencyAdmin, AgencyId = agency.Id });
            var provider = new Provider { AgencyId = agency.Id, FullName = "Sari Wulandari", Age = 30, Bio = "Berpengalaman lebih dari lima tahun.", ApplicationStatus = ProviderApplicationStatus.Draft, VerificationStatus = VerificationStatus.Pending, VillageId = VillageId, AddressDetail = "Alamat detail yang cukup panjang", PostalCode = "12345", Price = 50000, PricingType = PricingType.PerVisit };
            provider.Credential = new ProviderCredential { ProviderId = provider.Id, Provider = provider, Email = $"{tag}-provider@example.test", PasswordHash = "x", EmailVerifiedAt = DateTimeOffset.UtcNow };
            provider.Categories.Add(new ProviderCategory { ProviderId = provider.Id, ServiceCategoryId = categoryId });
            provider.Skills.Add(new ProviderSkill { ProviderId = provider.Id, SkillName = "Memasak" });
            provider.Languages.Add(new ProviderLanguage { ProviderId = provider.Id, LanguageName = "Indonesia" });
            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
                provider.Availability.Add(new ProviderAvailability { ProviderId = provider.Id, DayOfWeek = day, IsAvailable = true });
            provider.Documents.Add(new ProviderDocument { ProviderId = provider.Id, DocumentType = "KTP", StorageKey = "k" });
            provider.Documents.Add(new ProviderDocument { ProviderId = provider.Id, DocumentType = "KK", StorageKey = "k2" });
            harness.Db.Providers.Add(provider);
            await harness.SaveAsync();
            CancellationToken ct = default;
            var repo = new MarketplaceRepository(harness.Db, new ProviderScope());
            var wilayah = new FakeWilayah(null);
            var providerSvc = new ProviderService(repo, new StaticActor(new Actor(provider.Id, UserRole.Provider, null, provider.Id)), wilayah, null!, null!, new PasswordService(), new AuthRepository(harness.Db), harness.Service);
            await providerSvc.SubmitApplication(ct);
            var platform = new StaticActor(new Actor(Guid.NewGuid(), UserRole.PlatformAdmin, null));
            var adminSvc = new ProviderService(repo, platform, wilayah, null!, null!, new PasswordService(), new AuthRepository(harness.Db), harness.Service);
            await adminSvc.Moderate(provider.Id, ProviderApplicationStatus.Approved, new ProviderModerationRequest(null), ct);
            await adminSvc.Moderate(provider.Id, ProviderApplicationStatus.Suspended, new ProviderModerationRequest("Pelanggaran berat"), ct);
            await adminSvc.Reactivate(provider.Id, ct);
            var subjects = SentSubjects(harness, tag);
            Assert.Contains("[Bantu-Bantu] Aplikasi Anda terkirim", subjects);
            Assert.Contains("[Bantu-Bantu] Aplikasi Anda disetujui", subjects);
            Assert.Contains("[Bantu-Bantu] Akun Anda ditangguhkan", subjects);
            Assert.Contains("[Bantu-Bantu] Akun Anda aktif kembali", subjects);
            var suspended = harness.Sender.Sent.Single(m => m.Subject == "[Bantu-Bantu] Akun Anda ditangguhkan");
            Assert.Contains("Pelanggaran berat", suspended.Html);
            var agencyNotice = harness.Sender.Sent.Single(m => m.To == $"{tag}-agency-admin@example.test");
            Assert.Equal("[Bantu-Bantu] Aplikasi baru dari Sari", agencyNotice.Subject);
            // Agency suspend/reactivate reaches that agency's admins.
            var agencySvc = new AgencyService(repo, platform, null!, null!, harness.Service);
            await agencySvc.Suspend(agency.Id, ct);
            await agencySvc.Reactivate(agency.Id, ct);
            var agencySubjects = harness.Sender.Sent.Where(m => m.To == $"{tag}-agency-admin@example.test").Select(m => m.Subject).ToArray();
            Assert.Contains($"[Bantu-Bantu] Agency {tag} Agency ditangguhkan", agencySubjects);
            Assert.Contains($"[Bantu-Bantu] Agency {tag} Agency aktif kembali", agencySubjects);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }

    [Fact]
    public async Task PasswordChangeEmitsSecurityMail()
    {
        const string tag = "nb-pw";
        var harness = NewHarness();
        try
        {
            var passwords = new PasswordService();
            var provider = new Provider { FullName = "Sari Wulandari", ApplicationStatus = ProviderApplicationStatus.Approved, VerificationStatus = VerificationStatus.Verified };
            provider.Credential = new ProviderCredential { ProviderId = provider.Id, Provider = provider, Email = $"{tag}-provider@example.test", PasswordHash = passwords.HashProvider("old-password-12345"), EmailVerifiedAt = DateTimeOffset.UtcNow };
            harness.Db.Providers.Add(provider);
            await harness.SaveAsync();
            var auth = new AuthService(new AuthRepository(harness.Db), null!, null!, passwords, null!, new TwoFactorSettings(), harness.Service, NullLoggerFactory.Instance);
            await auth.ChangeProviderPasswordAsync(provider.Id, "old-password-12345", "new-password-12345", default);
            var mail = Assert.Single(harness.Sender.Sent);
            Assert.Equal($"{tag}-provider@example.test", mail.To);
            Assert.Equal("[Bantu-Bantu] Kata sandi Anda diubah", mail.Subject);
            Assert.DoesNotContain("new-password-12345", mail.Html);
            Assert.DoesNotContain("new-password-12345", mail.Text);
            Assert.DoesNotContain("old-password-12345", mail.Html);
        }
        finally { await DeleteGraphAsync(harness.Db, tag); harness.Dispose(); }
    }
}
