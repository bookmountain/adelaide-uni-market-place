using Application.Auth.Commands.ActivateUser;
using Application.Auth.Commands.AuthenticateUser;
using Application.Auth.Commands.RegisterUser;
using Application.Auth.Commands.ResendActivationEmail;
using Application.Common.Interfaces;
using Application.UnitTests.Common;
using Domain.Entities.Users;
using Domain.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class AuthCommandHandlerTests
{
    private const string ActivationBaseUrl = "https://localhost:7123/api/auth/activate";

    [Fact]
    public async Task Register_creates_inactive_user_and_sends_activation_link()
    {
        await using var db = await TestDb.CreateAsync();
        var emailSender = new CapturingEmailSender();
        var handler = new RegisterUserCommandHandler(db.Context, emailSender);

        var response = await handler.Handle(NewRegisterCommand(), CancellationToken.None);

        var user = await db.Context.Users.SingleAsync();
        Assert.Equal("student@adelaide.edu.au", response.Email);
        Assert.Equal("student@adelaide.edu.au", user.Email);
        Assert.False(user.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(user.ActivationToken));
        Assert.True(user.ActivationTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(BCrypt.Net.BCrypt.Verify("ChangeMe123!", user.PasswordHash));
        Assert.Single(emailSender.SentMessages);
        Assert.Equal(user.Email, emailSender.SentMessages[0].Email);
        Assert.Contains($"{ActivationBaseUrl}?token={user.ActivationToken}", emailSender.SentMessages[0].ActivationLink);
    }

    [Fact]
    public async Task Login_returns_null_until_account_is_activated()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewInactiveUser();
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new AuthenticateUserCommandHandler(db.Context);

        var beforeActivation = await handler.Handle(
            new AuthenticateUserCommand(user.Email, "ChangeMe123!"),
            CancellationToken.None);

        user.Activate();
        await db.Context.SaveChangesAsync();

        var afterActivation = await handler.Handle(
            new AuthenticateUserCommand(user.Email, "ChangeMe123!"),
            CancellationToken.None);

        Assert.Null(beforeActivation);
        Assert.NotNull(afterActivation);
        Assert.Equal(user.Id, afterActivation.UserId);
    }

    [Fact]
    public async Task Activate_valid_token_activates_user_and_clears_token()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewInactiveUser();
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var handler = new ActivateUserCommandHandler(db.Context);

        var result = await handler.Handle(new ActivateUserCommand("activation-token"), CancellationToken.None);

        var savedUser = await db.Context.Users.SingleAsync();
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.True(savedUser.IsActive);
        Assert.Null(savedUser.ActivationToken);
        Assert.Null(savedUser.ActivationTokenExpiresAt);
    }

    [Fact]
    public async Task ResendActivation_rotates_token_and_sends_new_link()
    {
        await using var db = await TestDb.CreateAsync();
        var user = NewInactiveUser();
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();
        var emailSender = new CapturingEmailSender();
        var handler = new ResendActivationEmailCommandHandler(db.Context, emailSender);

        var response = await handler.Handle(
            new ResendActivationEmailCommand(user.Email, ActivationBaseUrl),
            CancellationToken.None);

        var savedUser = await db.Context.Users.SingleAsync();
        Assert.Equal(user.Email, response.Email);
        Assert.NotEqual("activation-token", savedUser.ActivationToken);
        Assert.False(string.IsNullOrWhiteSpace(savedUser.ActivationToken));
        Assert.Single(emailSender.SentMessages);
        Assert.Contains($"token={savedUser.ActivationToken}", emailSender.SentMessages[0].ActivationLink);
    }

    private static RegisterUserCommand NewRegisterCommand() => new(
        Email: "student@adelaide.edu.au",
        Password: "ChangeMe123!",
        DisplayName: "Local Student",
        AvatarUrl: null,
        Department: AdelaideDepartment.ComputerScience,
        Degree: AcademicDegree.Bachelor,
        Sex: UserSex.Other,
        Nationality: Nationality.Australia,
        Age: 21,
        AllowedDomain: "adelaide.edu.au",
        ActivationBaseUrl: ActivationBaseUrl);

    private static User NewInactiveUser() => new(
        id: Guid.NewGuid(),
        email: "student@adelaide.edu.au",
        displayName: "Local Student",
        createdAt: DateTimeOffset.UtcNow,
        role: "Student",
        passwordHash: BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
        department: AdelaideDepartment.ComputerScience,
        degree: AcademicDegree.Bachelor,
        sex: UserSex.Other,
        nationality: Nationality.Australia,
        age: 21,
        isActive: false,
        activationToken: "activation-token",
        activationTokenExpiresAt: DateTimeOffset.UtcNow.AddHours(24));

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string Email, string ActivationLink)> SentMessages { get; } = [];

        public Task SendAccountActivationAsync(
            string email,
            string activationLink,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add((email, activationLink));
            return Task.CompletedTask;
        }
    }

}
