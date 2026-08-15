using Configuration;
using Configuration.Extensions;
using Configuration.Options;
using DbContext.Extensions;
using DbRepos;

using Services;

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

#region Initializing the standard sw stack using extensions
builder.Configuration.AddSecrets(builder.Environment);
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddVersionInfo();
builder.Services.AddInMemoryLogger();
builder.Services.AddUserBasedDbContext();
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


//Inject DbRepos and Services
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();

builder.Services.AddScoped<IAdminService, AdminServiceDb>();
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();



var app = builder.Build();

// Configure the HTTP request pipeline.
// for the purpose of this example, we will use Swagger also in production
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Seido Friends API v2.0");
    });
}

app.UseHttpsRedirection();
app.UseCors(); 

app.UseAuthorization();
app.MapControllers();

app.Run();
