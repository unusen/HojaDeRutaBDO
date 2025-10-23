using HojaDeRuta.DBContext;
using HojaDeRuta.Helpers;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Services;
using HojaDeRuta.Services.AutoMapper;
using HojaDeRuta.Services.LoginService;
using HojaDeRuta.Services.Repository;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HojasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("hojaDB")));

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
            //options.ResponseType = "code";
            //options.Prompt = "consent";
        })
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddDistributedTokenCaches();
//.AddInMemoryTokenCaches();


var cookieSettings = builder.Configuration.GetSection("CookieSettings");
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = cookieSettings.GetValue<string>("Name");
    options.ExpireTimeSpan = TimeSpan.FromHours(cookieSettings.GetValue<int>("ExpireHours"));
    options.SlidingExpiration = cookieSettings.GetValue<bool>("SlidingExpiration");

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
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

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<CreatioService>();
builder.Services.AddScoped<HojaDeRutaService>();
builder.Services.AddScoped<SharedService>();
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<RevisorService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddHostedService<SyncService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<MappingProfile>();
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();