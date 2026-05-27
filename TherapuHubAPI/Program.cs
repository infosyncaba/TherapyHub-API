using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services;
using TherapuHubAPI.Services.Implementations;
using TherapuHubAPI.Services.IServices;


AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ContextDB>(options =>
    options.UseNpgsql(connectionString));

// Repository Pattern
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddScoped<ITipoUsuarioRepositorio, TipoUsuarioRepositorio>();
builder.Services.AddScoped<IMenuRepositorio, MenuRepositorio>();
builder.Services.AddScoped<ITipoEventoRepositorio, TipoEventoRepositorio>();
builder.Services.AddScoped<IEventosRepositorio, EventosRepositorio>();
builder.Services.AddScoped<IEventoUsuariosRepositorio, EventoUsuariosRepositorio>();
builder.Services.AddScoped<ICompaniaRepositorio, CompaniaRepositorio>();
builder.Services.AddScoped<ICompanyChatsRepositorio, CompanyChatsRepositorio>();
builder.Services.AddScoped<IChatMessagesRepositorio, ChatMessagesRepositorio>();
builder.Services.AddScoped<IMessageReadsRepositorio, MessageReadsRepositorio>();
builder.Services.AddScoped<IStaffRepositorio, StaffRepositorio>();
builder.Services.AddScoped<IStaffStatusRepositorio, StaffStatusRepositorio>();
builder.Services.AddScoped<IStaffRolesRepositorio, StaffRolesRepositorio>();
builder.Services.AddScoped<IFolderRepositorio, FolderRepositorio>();
builder.Services.AddScoped<IFileRepositorio, FileRepositorio>();
builder.Services.AddScoped<IClientRepositorio, ClientRepositorio>();
builder.Services.AddScoped<IClientStatusRepositorio, ClientStatusRepositorio>();
builder.Services.AddScoped<IGoalTrackerStatusRepositorio, GoalTrackerStatusRepositorio>();
builder.Services.AddScoped<ISessionNotesStatusRepositorio, SessionNotesStatusRepositorio>();
builder.Services.AddScoped<IGoalTrackerCategoriesRepositorio, GoalTrackerCategoriesRepositorio>();
builder.Services.AddScoped<IGoalTrackersRepositorio, GoalTrackersRepositorio>();
builder.Services.AddScoped<IGoalTrackerItemsRepositorio, GoalTrackerItemsRepositorio>();
builder.Services.AddScoped<ILibraryItemRepositorio, LibraryItemRepositorio>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITipoUsuarioService, TipoUsuarioService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ITipoEventoService, TipoEventoService>();
builder.Services.AddScoped<IEventosService, EventosService>();
builder.Services.AddScoped<ICompaniaService, CompaniaService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IEntityFilesService, EntityFilesService>();
builder.Services.AddScoped<IStaffDocumentService, StaffDocumentService>();
builder.Services.AddScoped<INotesService, NotesService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IGoalTrackerStatusService, GoalTrackerStatusService>();
builder.Services.AddScoped<ISessionNotesStatusService, SessionNotesStatusService>();
builder.Services.AddScoped<IGoalTrackerService, GoalTrackerService>();
builder.Services.AddScoped<ISessionNotesService, SessionNotesService>();
builder.Services.AddScoped<IActorRelationshipService, ActorRelationshipService>();
builder.Services.AddScoped<INoteCategoryService, NoteCategoryService>();
builder.Services.AddScoped<ILibraryItemService, LibraryItemService>();
builder.Services.AddSingleton<IFileStorageService, CloudflareR2StorageService>();

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key no configurada");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TherapuHubAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TherapuHubAPI";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:8080",
                "http://localhost:8082",
                "http://localhost:5173",
                "https://localhost:8080",
                "https://localhost:8082",
                "https://localhost:5173",
                "http://127.0.0.1:8080",
                "http://127.0.0.1:8082",
                "http://127.0.0.1:5173",
                "https://127.0.0.1:8080",
                "https://127.0.0.1:8082",
                "https://127.0.0.1:5173",
                "https://therapyhub-suite.vercel.app",
                "https://syncaba-api.onrender.com",
                "https://syncaba.net",
                "https://www.syncaba.net"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Controllers
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TherapuHub API",
        Version = "v1",
        Description = "API para TherapuHub"
    });

    // Configurar JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TherapuHub API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
