using Domain.Entities.Items;
using Domain.Entities.Orders;
using Domain.Shared.Enums;

namespace Domain.Entities.Users;

public class User
{
    private User()
    {
    }

    public User(
        Guid id,
        string email,
        string displayName,
        DateTimeOffset createdAt,
        string role,
        string passwordHash,
        AdelaideDepartment department,
        AcademicDegree degree,
        UserSex sex,
        string? avatarUrl = null,
        Nationality? nationality = null,
        int? age = null,
        bool isActive = false,
        string? activationToken = null,
        DateTimeOffset? activationTokenExpiresAt = null)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
        Role = role;
        PasswordHash = passwordHash;
        Department = department;
        Degree = degree;
        Sex = sex;
        AvatarUrl = avatarUrl;
        Nationality = nationality;
        Age = age;
        IsActive = isActive;
        ActivationToken = activationToken;
        ActivationTokenExpiresAt = activationTokenExpiresAt;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public AdelaideDepartment Department { get; private set; }
    public AcademicDegree Degree { get; private set; }
    public UserSex Sex { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Nationality? Nationality { get; private set; }
    public int? Age { get; private set; }
    public bool IsActive { get; private set; }
    public string? ActivationToken { get; private set; }
    public DateTimeOffset? ActivationTokenExpiresAt { get; private set; }

    public ICollection<Item> ItemsForSale { get; } = new List<Item>();
    public ICollection<Order> Orders { get; } = new List<Order>();

    public void UpdateProfile(
        string displayName,
        string role,
        AdelaideDepartment department,
        AcademicDegree degree,
        UserSex sex,
        string? avatarUrl,
        Nationality? nationality,
        int? age)
    {
        DisplayName = displayName;
        Role = role;
        Department = department;
        Degree = degree;
        Sex = sex;
        AvatarUrl = avatarUrl;
        Nationality = nationality;
        Age = age;
    }

    public void UpdatePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void SetActivation(string token, DateTimeOffset expiresAt)
    {
        ActivationToken = token;
        ActivationTokenExpiresAt = expiresAt;
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
        ActivationToken = null;
        ActivationTokenExpiresAt = null;
    }
}
