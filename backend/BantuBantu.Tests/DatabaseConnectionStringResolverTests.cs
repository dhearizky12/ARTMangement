using BantuBantu.Infrastructure;
using Npgsql;

namespace BantuBantu.Tests;

public sealed class DatabaseConnectionStringResolverTests
{
    [Fact]
    public void ConvertsNeonUriAndPreservesTlsSettings()
    {
        var result = DatabaseConnectionStringResolver.Convert(
            "postgresql://app%40user:p%40ss%21@ep-example-pooler.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require&application_name=bantu-bantu");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("ep-example-pooler.us-east-2.aws.neon.tech", builder.Host);
        Assert.Equal("app@user", builder.Username);
        Assert.Equal("p@ss!", builder.Password);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
        Assert.Equal("bantu-bantu", builder.ApplicationName);
    }

    [Fact]
    public void RequiresTlsForNeonKeyValueConnectionStringsWhenSslModeIsOmitted()
    {
        var result = DatabaseConnectionStringResolver.Convert(
            "Host=ep-example.us-east-2.aws.neon.tech;Database=neondb;Username=app;Password=secret");

        Assert.Equal(SslMode.Require, new NpgsqlConnectionStringBuilder(result).SslMode);
    }

    [Fact]
    public void LeavesRegularPostgresConnectionStringsUsable()
    {
        var result = DatabaseConnectionStringResolver.Convert(
            "Host=localhost;Port=55449;Database=test;Username=tester;Password=secret");

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("localhost", builder.Host);
        Assert.Equal(55449, builder.Port);
        Assert.Equal(SslMode.Prefer, builder.SslMode);
    }
}
