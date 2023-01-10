using Microsoft.Extensions.Options;

namespace QuickAzTables.AspNetIntegrationBasic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddSingleton((services) =>
            {
                // this connection string must be specified in appsettings.json
                var connectionString = builder
                                        .Configuration
                                        .GetConnectionString("TableStorage");
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new Exception($"Could not find connection string. " +
                        $"This could be because the connection string was not specified in config.");
                }

                return new TypedTableStore<Car>(tableName: "Cars",
                                                connectionString: connectionString,
                                                partitionKeySelector: c => c.City,
                                                rowKeySelector: c => c.PlateNo);
            });

            /// Add MVC since we have a controller to demo See <see cref="CarController"/>.
            builder.Services.AddMvc();

            // Add services to the container.
            builder.Services.AddRazorPages();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();

            /// Map Controllers for our <see cref="CarController"/> works.
            app.MapControllers();
            app.Run();
        }
    }
}
