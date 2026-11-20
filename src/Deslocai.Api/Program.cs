using System.Reflection;
using System.Text.Json.Serialization;
using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Deslocai.Api.Domain.Regras;
using Deslocai.Api.Infrastructure;
using Deslocai.Api.Middleware;
using Deslocai.Security;
using Deslocai.Security.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;


if (args.Contains("--selftest")) return Deslocai.Security.Tests.SelfTest.Run();

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<DeslocaiDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=deslocai.db"));


builder.Services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));

builder.Services.AddSingleton<IRegraAlerta, RegraSuperlotacao>();
builder.Services.AddSingleton<IRegraAlerta, RegraCalorExtremo>();

builder.Services.AddScoped<IEstacaoService, EstacaoService>();
builder.Services.AddScoped<IEquipamentoService, EquipamentoService>();
builder.Services.AddScoped<ILeituraService, LeituraService>();
builder.Services.AddScoped<IAlertaService, AlertaService>();
builder.Services.AddScoped<IRelatoService, RelatoService>();
builder.Services.AddScoped<IIndicadorService, IndicadorService>();


builder.Services.AddDeslocaSecurity();
builder.Services.AddScoped<IDescricaoSanitizer, DescricaoSanitizer>();  


builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<TratadorDeExcecoes>();
builder.Services.AddProblemDetails();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Deslocai API",
        Version = "v1",
        Description = "Backend da CitizenTech Deslocai: transporte público mais previsível, confortável e acessível."
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

using (var escopo = app.Services.CreateScope())
{
    await DbInitializer.InicializarAsync(escopo.ServiceProvider.GetRequiredService<DeslocaiDbContext>());
}

app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.UseDeslocaSecurity();   
app.MapControllers();

app.Run();
return 0;
