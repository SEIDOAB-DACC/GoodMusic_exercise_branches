using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);

// NOTE: global cors policy needed for JS and React frontends
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers().AddNewtonsoftJson(options =>
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
builder.Services.AddEndpointsApiExplorer();

#region Initializing the standard sw stack
//using user secrets
//You dont need to use the assembly (reflection) to access user secrets at runtime
//But you need to use the assembly (reflection) to access user secrets at design time (efc migrations)
//In later branches this code is refactored into a configuration extension that is used both at design time and runtime
//to switch between user secrets (development) and azure key vault (production)
var currentDir = Directory.GetCurrentDirectory();
var assembly = System.Reflection.Assembly.Load("Configuration");
builder.Configuration.SetBasePath(Path.Combine(currentDir, "../AppWebApi"))
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddUserSecrets(assembly);

// adding options patterns to read appsettings and user secrets
builder.Services.Configure<AesEncryptionOptions>(
    options => builder.Configuration.GetSection(AesEncryptionOptions.Position).Bind(options));

builder.Services.Configure<JwtOptions>(
    options => builder.Configuration.GetSection(JwtOptions.Position).Bind(options));

// adding options and service for multiple Database connections and their respective DbContexts
builder.Services.Configure<DbConnectionSetsOptions>(
    options => builder.Configuration.GetSection(DbConnectionSetsOptions.Position).Bind(options));

// adding version info
builder.Services.Configure<VersionOptions>(options =>VersionOptions.ReadFromAssembly(options));
#endregion

builder.Services.Configure<MySecretOptions>(
    options => builder.Configuration.GetSection(MySecretOptions.Position).Bind(options));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Seido Music simple API",
#if DEBUG
        Version = "v2.0 DEBUG",
#else
        Version = "v2.0",
#endif
        Description = "This is an API used in Seido's various software developer training courses."
        + $"<br>DataSet: {builder.Configuration["DatabaseConnections:UseDataSetWithTag"]}"
        + $"<br>DefaultDataUser: {builder.Configuration["DatabaseConnections:DefaultDataUser"]}"
    });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
// for the purpose of this example, we will use Swagger also in production
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Seido Music simple API v2.0");
    });
}

app.UseHttpsRedirection();
app.UseCors(); 

app.UseAuthorization();
app.MapControllers();

app.Run();
