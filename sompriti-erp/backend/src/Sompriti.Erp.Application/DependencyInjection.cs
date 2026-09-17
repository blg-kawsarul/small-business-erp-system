using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sompriti.Erp.Application.Auth;
using Sompriti.Erp.Application.Common;
using Sompriti.Erp.Application.MasterData;
using Sompriti.Erp.Application.Orders;
using Sompriti.Erp.Application.Reports;
using Sompriti.Erp.Application.Sms;
using Sompriti.Erp.Application.Stock;
using Sompriti.Erp.Application.Users;

namespace Sompriti.Erp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
        services.Configure<AppOptions>(config.GetSection(AppOptions.Section));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CompanyService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<SalesOrderService>();
        services.AddScoped<ReportService>();
        services.AddScoped<SmsService>();
        return services;
    }
}
