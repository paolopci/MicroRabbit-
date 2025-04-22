using MediatR; 
using Microsoft.EntityFrameworkCore;
using MicroRabbit.Banking.Data.Context;
using MicroRabbit.Banking.Data.Repository;
using MicroRabbit.Infra.IoC;
using MicroRabbit.Banking.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1) Registrazione di MediatR (scannerizza l'assembly corrente)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly)
);

// 2) Registrazione dei repository
builder.Services.AddTransient<IAccountRepository, AccountRepository>();

// 3) Registrazione del DbContext con la stringa di connessione
builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BankingDbConnection"))
);

// 4) Registrazione dei servizi custom (EventBus, AccountService, ecc.)
DependencyContainer.RegisterServices(builder.Services);

// 5) Framework services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Banking Microservice", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json","Banking Microservice V1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
