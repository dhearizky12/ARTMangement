using System.ComponentModel.DataAnnotations;
using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BantuBantu.Infrastructure;

public sealed record DemoAccountSeedOptions(
    string PlatformAdminEmail,
    string PlatformAdminPassword,
    string AgencyAdminEmail,
    string AgencyAdminPassword,
    string AgencyAdminFullName,
    string AgencyName,
    string AgencyContactInfo,
    string ProviderEmail,
    string ProviderPassword,
    string ProviderFullName,
    string CustomerEmail,
    string CustomerFullName,
    string CustomerGoogleSubject)
{
    public static DemoAccountSeedOptions FromConfiguration(IConfiguration configuration)
    {
        static string Required(IConfiguration configuration, string key)
        {
            var value = configuration[key]?.Trim();
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"Missing configuration: {key}");
            return value;
        }

        static string Optional(IConfiguration configuration, string key, string fallback) =>
            string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!.Trim();

        static string Password(IConfiguration configuration, string roleKey)
        {
            var shared = configuration["SeedAccounts:SharedPassword"]?.Trim();
            return string.IsNullOrWhiteSpace(shared) ? Required(configuration, roleKey) : shared;
        }

        return new(
            Optional(configuration, "SeedAccounts:PlatformAdmin:Email", "platform.admin@bantubantu.local"),
            Password(configuration, "SeedAccounts:PlatformAdmin:Password"),
            Optional(configuration, "SeedAccounts:AgencyAdmin:Email", "agency.admin@bantubantu.local"),
            Password(configuration, "SeedAccounts:AgencyAdmin:Password"),
            Optional(configuration, "SeedAccounts:AgencyAdmin:FullName", "Demo Agency Admin"),
            Optional(configuration, "SeedAccounts:Agency:Name", "Demo Bantu-Bantu Agency"),
            Optional(configuration, "SeedAccounts:Agency:ContactInfo", "demo-agency@bantubantu.local"),
            Optional(configuration, "SeedAccounts:Provider:Email", "provider@bantubantu.local"),
            Password(configuration, "SeedAccounts:Provider:Password"),
            Optional(configuration, "SeedAccounts:Provider:FullName", "Demo Provider"),
            Optional(configuration, "SeedAccounts:Customer:Email", "customer@bantubantu.local"),
            Optional(configuration, "SeedAccounts:Customer:FullName", "Demo Customer"),
            Required(configuration, "SeedAccounts:Customer:GoogleSubject"));
    }
}

public sealed class DemoAccountSeeder(AppDbContext db, IPasswordService passwords)
{
    public async Task SeedAsync(DemoAccountSeedOptions options, CancellationToken cancellationToken = default)
    {
        Validate(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var agency = await EnsureAgencyAsync(options, cancellationToken);
        await EnsurePlatformAdminAsync(options, cancellationToken);
        await EnsureAgencyAdminAsync(options, agency, cancellationToken);
        await EnsureProviderAsync(options, cancellationToken);
        await EnsureCustomerAsync(options, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Agency> EnsureAgencyAsync(DemoAccountSeedOptions options, CancellationToken cancellationToken)
    {
        var agency = await db.Agencies.SingleOrDefaultAsync(x => x.Name == options.AgencyName, cancellationToken);
        if (agency is null)
        {
            agency = new Agency
            {
                Name = options.AgencyName,
                ContactInfo = options.AgencyContactInfo,
                Status = AgencyStatus.Approved
            };
            db.Agencies.Add(agency);
            return agency;
        }

        if (agency.Status != AgencyStatus.Approved)
            throw new InvalidOperationException($"Agency '{options.AgencyName}' already exists but is not Approved.");
        return agency;
    }

    private async Task EnsurePlatformAdminAsync(DemoAccountSeedOptions options, CancellationToken cancellationToken)
    {
        var existing = await db.AdminAccounts.SingleOrDefaultAsync(x => x.Email == options.PlatformAdminEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != UserRole.PlatformAdmin || existing.AgencyId is not null)
                throw new InvalidOperationException($"Seed account email is already used by a different role: {options.PlatformAdminEmail}");
            return;
        }

        if (await db.Users.AnyAsync(x => x.Email == options.PlatformAdminEmail, cancellationToken))
            throw new InvalidOperationException($"Seed account email is already used by a different User: {options.PlatformAdminEmail}");

        var account = new AdminAccount
        {
            Email = options.PlatformAdminEmail,
            FullName = "Platform Admin",
            Role = UserRole.PlatformAdmin,
            ProfileCompleted = true
        };
        account.PasswordHash = passwords.Hash(account, options.PlatformAdminPassword);
        db.AdminAccounts.Add(account);
    }

    private async Task EnsureAgencyAdminAsync(DemoAccountSeedOptions options, Agency agency, CancellationToken cancellationToken)
    {
        var existing = await db.AdminAccounts.SingleOrDefaultAsync(x => x.Email == options.AgencyAdminEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != UserRole.AgencyAdmin || existing.AgencyId != agency.Id)
                throw new InvalidOperationException($"Seed account email is already used by a different role: {options.AgencyAdminEmail}");
            return;
        }

        if (await db.Users.AnyAsync(x => x.Email == options.AgencyAdminEmail, cancellationToken))
            throw new InvalidOperationException($"Seed account email is already used by a different User: {options.AgencyAdminEmail}");

        var account = new AdminAccount
        {
            Email = options.AgencyAdminEmail,
            FullName = options.AgencyAdminFullName,
            Role = UserRole.AgencyAdmin,
            AgencyId = agency.Id,
            Agency = agency,
            ProfileCompleted = true
        };
        account.PasswordHash = passwords.Hash(account, options.AgencyAdminPassword);
        db.AdminAccounts.Add(account);
    }

    private async Task EnsureProviderAsync(DemoAccountSeedOptions options, CancellationToken cancellationToken)
    {
        var existing = await db.ProviderCredentials.Include(x => x.Provider).SingleOrDefaultAsync(x => x.Email == options.ProviderEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Provider.ApplicationStatus == ProviderApplicationStatus.Suspended)
                throw new InvalidOperationException($"Seed Provider is suspended: {options.ProviderEmail}");
            return;
        }

        if (await db.Users.AnyAsync(x => x.Email == options.ProviderEmail, cancellationToken))
            throw new InvalidOperationException($"Seed account email is already used by a User: {options.ProviderEmail}");

        var provider = new Provider
        {
            FullName = options.ProviderFullName,
            ApplicationStatus = ProviderApplicationStatus.Approved,
            VerificationStatus = VerificationStatus.Verified,
            IdentityVerified = true,
            BackgroundCheckPassed = true,
            ContractSigned = true
        };
        provider.Credential = new ProviderCredential
        {
            ProviderId = provider.Id,
            Provider = provider,
            Email = options.ProviderEmail,
            PasswordHash = passwords.HashProvider(options.ProviderPassword)
        };
        db.Providers.Add(provider);
    }

    private async Task EnsureCustomerAsync(DemoAccountSeedOptions options, CancellationToken cancellationToken)
    {
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == options.CustomerEmail, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Customer)
                throw new InvalidOperationException($"Seed account email is already used by a different role: {options.CustomerEmail}");

            var login = await db.ExternalLogins.SingleOrDefaultAsync(x => x.UserId == existing.Id && x.Provider == "Google", cancellationToken);
            if (login is null)
            {
                db.ExternalLogins.Add(new ExternalLogin { UserId = existing.Id, User = existing, ProviderKey = options.CustomerGoogleSubject });
            }
            else if (login.ProviderKey != options.CustomerGoogleSubject)
            {
                throw new InvalidOperationException($"Customer GoogleSubject does not match the existing login: {options.CustomerEmail}");
            }
            return;
        }

        if (await EmailUsedByProviderAsync(options.CustomerEmail, cancellationToken))
            throw new InvalidOperationException($"Seed account email is already used by a Provider: {options.CustomerEmail}");

        var customer = new User { Email = options.CustomerEmail, FullName = options.CustomerFullName, Role = UserRole.Customer, ProfileCompleted = true };
        db.Users.Add(customer);
        db.ExternalLogins.Add(new ExternalLogin
        {
            UserId = customer.Id,
            User = customer,
            Provider = "Google",
            ProviderKey = options.CustomerGoogleSubject
        });
    }

    private Task<bool> EmailUsedByProviderAsync(string email, CancellationToken cancellationToken) =>
        db.ProviderCredentials.AnyAsync(x => x.Email == email, cancellationToken);

    private static void Validate(DemoAccountSeedOptions options)
    {
        var emails = new[] { options.PlatformAdminEmail, options.AgencyAdminEmail, options.ProviderEmail, options.CustomerEmail };
        if (emails.Any(email => !new EmailAddressAttribute().IsValid(email))) throw new InvalidOperationException("All SeedAccounts email values must be valid email addresses.");
        if (options.PlatformAdminPassword.Length < 14 || options.AgencyAdminPassword.Length < 14 || options.ProviderPassword.Length < 14)
            throw new InvalidOperationException("SeedAccounts passwords must be at least 14 characters.");
        if (options.CustomerGoogleSubject.Length < 6) throw new InvalidOperationException("SeedAccounts:Customer:GoogleSubject is invalid.");
        if (emails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != emails.Length)
            throw new InvalidOperationException("SeedAccounts emails must be unique across roles.");
    }
}
