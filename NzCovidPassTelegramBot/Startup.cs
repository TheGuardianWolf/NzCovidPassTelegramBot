using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NzCovidPass.Core.Shared;
using NzCovidPassTelegramBot.Data.Shared;
using NzCovidPassTelegramBot.Repositories;
using NzCovidPassTelegramBot.Services;
using NzCovidPassTelegramBot.Services.Hosted;
using Serilog;
using System.Net.Mime;
using Telegram.Bot;


namespace NzCovidPassTelegramBot
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Environment = env;
            Configuration = configuration;
//#pragma warning disable CS0618 // Type or member is obsolete
//            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
//#pragma warning restore CS0618 // Type or member is obsolete
//            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }

        public void ConfigureServices(IServiceCollection services)
        {

            var telegramConfig = Configuration.GetSection("Telegram").Get<TelegramConfiguration>();

            // Add services to the container.
            services.AddRazorPages(options =>
            {
                options.RootDirectory = "/View/Pages";
            });
            services.AddServerSideBlazor();

            // App
            services.AddHostedService<ConfigureTelegramWebhookHostedService>();
            services.AddSingleton<IInlineMessagePollRepository, InlineMessagePollRepository>();
            services.AddSingleton<ICovidPassRepository, CovidPassRepository>();

            services.AddScoped<ITelegramBotService, TelegramBotService>();
            services.AddScoped<ICovidPassLinkerService, CovidPassLinkerService>();
            services.AddScoped<ICovidPassPollService, CovidPassPollService>();

            // 3rd Party
            var hasRedisConfig = !string.IsNullOrEmpty(Configuration.GetConnectionString("Cache"));
            if (hasRedisConfig)
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.InstanceName = typeof(Program).FullName;
                    options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(Configuration.GetConnectionString("Cache"));
                    options.ConfigurationOptions.CertificateValidation += ConfigurationOptions_CertificateValidation;
                });
            }
            else
            {
                services.AddDistributedMemoryCache();
            }

            services.AddNzCovidPassVerifier();
            services.AddMemoryCache(); // Needed for covid pass
            services.AddHttpClient("tgwebhook")
                   .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(telegramConfig.BotToken, httpClient));

            services.AddControllers().AddNewtonsoftJson();


        }

        private bool ConfigurationOptions_CertificateValidation(object sender, System.Security.Cryptography.X509Certificates.X509Certificate? certificate, System.Security.Cryptography.X509Certificates.X509Chain? chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Configure the HTTP request pipeline.
            if (!env.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSerilogRequestLogging();

            app.UseStatusCodePages("application/json", "{0}");

            //app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapControllers();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}
