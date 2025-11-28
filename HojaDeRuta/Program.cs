using HojaDeRuta.DBContext;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Services;
using HojaDeRuta.Services.AutoMapper;
using HojaDeRuta.Services.LoginService;
using HojaDeRuta.Services.Repository;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

//builder.Services.AddDbContext<HojasDbContext>(options =>
//    options.UseSqlServer(builder.Configuration.GetConnectionString("hojaDB")));
builder.Services.AddDbContext<HojasDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("hojaDB"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
        }
));

builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("hojaDB");
    options.SchemaName = "dbo";
    options.TableName = "TokenCache";
});

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(options =>
    {
        builder.Configuration.Bind("AzureAd", options);

        // ⚠ IMPORTANTE PARA DOCKER DETRÁS DE NGINX
        options.RequireHttpsMetadata = false;
        options.CallbackPath = "/signin-oidc";

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            // Respeta el protocolo HTTPS que viene desde NGINX
            var proto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            if (!string.IsNullOrEmpty(proto))
                context.ProtocolMessage.RedirectUri = $"{proto}://{context.Request.Host}{options.CallbackPath}";

            return Task.CompletedTask;
        };

        options.Events.OnTokenValidated = async context =>
        {
            var _loginService = context.HttpContext.RequestServices.
                GetRequiredService<ILoginService>();

            var _userService = context.HttpContext.RequestServices
                .GetRequiredService<UserService>();

            var userName = _loginService.GetUserName();

            await _userService.ValidateUserAsync(userName);
        };
    })
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddDistributedTokenCaches();


//builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
//        .AddMicrosoftIdentityWebApp(options =>
//        {
//            builder.Configuration.Bind("AzureAd", options);
//            //options.ResponseType = "code";
//            //options.Prompt = "consent";

//            options.Events.OnTokenValidated = async context =>
//            {
//                var _loginService = context.HttpContext.RequestServices.
//                    GetRequiredService<ILoginService>();

//                var _userService = context.HttpContext.RequestServices
//                    .GetRequiredService<UserService>();

//                var userName = _loginService.GetUserName();

//                await _userService.ValidateUserAsync(userName);
//            };
//        })
//    .EnableTokenAcquisitionToCallDownstreamApi()
//    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
//    .AddDistributedTokenCaches();
////.AddInMemoryTokenCaches();


var cookieSettings = builder.Configuration.GetSection("CookieSettings");

//builder.Services.ConfigureApplicationCookie(options =>
//{
//    options.Cookie.Name = cookieSettings.GetValue<string>("Name");
//    options.ExpireTimeSpan = TimeSpan.FromHours(cookieSettings.GetValue<int>("ExpireHours"));
//    options.SlidingExpiration = cookieSettings.GetValue<bool>("SlidingExpiration");

//    options.Cookie.HttpOnly = true;
//    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//    options.Cookie.SameSite = SameSiteMode.Strict;
//});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = cookieSettings.GetValue<string>("Name");
    options.ExpireTimeSpan = TimeSpan.FromHours(cookieSettings.GetValue<int>("ExpireHours"));
    options.SlidingExpiration = cookieSettings.GetValue<bool>("SlidingExpiration");

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    options.Cookie.SameSite = SameSiteMode.None;
});


//builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration);

builder.Services.AddMicrosoftGraph(options =>
{
    options.Scopes = "https://graph.microsoft.com/.default";
});


//builder.Services.AddAuthorization();

builder.Services.AddAuthorization(options =>
{
    var groups = builder.Configuration.GetSection("Groups");

    options.AddPolicy("RequireManagingPartner", policy =>
        policy.RequireClaim("groups", groups["HDR_Managing_Partner"]));

    options.AddPolicy("RequireOficialesDeRiesgo", policy =>
        policy.RequireClaim("groups", groups["HDR_Oficiales_de_Riesgo"]));

    options.AddPolicy("RequireSocioLiderDeArea", policy =>
        policy.RequireClaim("groups", groups["HDR_Socio_líder_de_area"]));
});

//builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();

builder.Services.AddControllersWithViews(options =>
{
    //TODO: ACTIVAR FILTRO DE GRUPO REQUERIDO (DE ACUERDO A GESTION DE GRUPOS)
    //options.Filters.Add<RequireGroupsFilter>();
}).AddMicrosoftIdentityUI();

builder.Services.Configure<SyncSettings>(builder.Configuration.GetSection("SyncSettings"));
builder.Services.Configure<GroupsSettings>(builder.Configuration.GetSection("GroupsSettings"));
builder.Services.Configure<DBSettings>(builder.Configuration.GetSection("DBSettings"));
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.Configure<MonedasSettings>(builder.Configuration.GetSection("MonedasSettings"));
builder.Services.Configure<PathSetings>(builder.Configuration.GetSection("PathSetings"));

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<CreatioService>();
builder.Services.AddScoped<HojaDeRutaService>();
builder.Services.AddScoped<SharedService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<RevisorService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddHostedService<SyncService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});

//TODO: QUITAR EN PROD
builder.Logging.AddConsole();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();  // Permite cualquier proxy
    options.KnownProxies.Clear();
});

builder.Configuration.AddEnvironmentVariables();


var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//TODO: SOLO DESACTIVADO PARA EL CONTENEDOR, ACTIVAR LOCALMENTE
//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

//app.UseMiddleware<UserContextMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();