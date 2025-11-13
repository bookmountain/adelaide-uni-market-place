using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;
using Application;
using Application.Common.Interfaces;
using Infrastructure.Configuration.Options;
using Infrastructure.Data;
using Infrastructure.Data.Seeding;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

const string AppJwtScheme = "AppJwt";

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
});

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Marketplace API",
        Version = "v1",
        Description = "Adelaide University Marketplace backend"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var authOptions = BindOptions<AuthOptions>(builder.Services, builder.Configuration);
var postgresOptions = BindOptions<PostgresOptions>(builder.Services, builder.Configuration);
var redisOptions = BindOptions<RedisOptions>(builder.Services, builder.Configuration);
_ = BindOptions<EmailOptions>(builder.Services, builder.Configuration);
_ = BindOptions<RabbitMqOptions>(builder.Services, builder.Configuration);
_ = BindOptions<ElasticsearchOptions>(builder.Services, builder.Configuration);
_ = BindOptions<StripeOptions>(builder.Services, builder.Configuration);
_ = BindOptions<R2Options>(builder.Services, builder.Configuration);

builder.Services.AddApplication();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services
    .AddDbContext<MarketplaceDbContext>(options =>
        options.UseNpgsql(postgresOptions.ConnectionString));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<MarketplaceDbContext>());
builder.Services.AddScoped<IEmailSender, Infrastructure.Email.SmtpEmailSender>();
builder.Services.AddSingleton<IObjectStorageService, R2ObjectStorageService>();
builder.Services.AddScoped<DatabaseSeeder>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = AppJwtScheme;
        options.DefaultChallengeScheme = AppJwtScheme;
    })
    .AddJwtBearer(AppJwtScheme, options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.AppJwtIssuer,
            ValidateAudience = true,
            ValidAudience = authOptions.AppJwtIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.AppJwtSigningKey)),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services
    .AddHealthChecks()
    .AddNpgSql(postgresOptions.ConnectionString, name: "postgres", tags: ["ready"])
    .AddRedis(redisOptions.ConnectionString, name: "redis", tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
    await dbContext.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Console.WriteLine("Swagger UI available at /swagger");
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapControllers();

Console.WriteLine("Starting application...");

await app.RunAsync();


static TOptions BindOptions<TOptions>(IServiceCollection services, IConfiguration configuration)
    where TOptions : class, IConfigSection, new()
{
    var section = configuration.GetSection(TOptions.SectionName);
    if (!section.Exists())
    {
        throw new InvalidOperationException($"Configuration section '{TOptions.SectionName}' is missing.");
    }

    services.AddOptions<TOptions>()
        .Bind(section)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    var options = section.Get<TOptions>() ?? new TOptions();
    ValidateOptions(options, TOptions.SectionName);

    return options;
}

static void ValidateOptions<TOptions>(TOptions options, string sectionName)
{
    var validationResults = new List<ValidationResult>();
    if (!Validator.TryValidateObject(options!, new ValidationContext(options!), validationResults, true))
    {
        var errors = string.Join(", ", validationResults.Select(result => result.ErrorMessage));
        throw new InvalidOperationException($"Configuration '{sectionName}' is invalid: {errors}");
    }
}
