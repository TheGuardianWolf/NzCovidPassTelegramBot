using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NzCovidPass.Core.Shared;
using NzCovidPassTelegramBot.Data.Bot;
using NzCovidPassTelegramBot.Repositories;
using NzCovidPassTelegramBot.Repositories.DataSources;
using NzCovidPassTelegramBot.Services;
using NzCovidPassTelegramBot.Services.Bot;
using NzCovidPassTelegramBot.Services.Hosted;
using Serilog;
using System.Net.Mime;
using System.Security.Authentication;
using Telegram.Bot;
using SendGrid.Extensions.DependencyInjection;
using System.Globalization;
using Texnomic.Blazor.hCaptcha.Extensions;

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
#pragma warning disable CS0618 // Type or member is obsolete
            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618 // Type or member is obsolete
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var dataInstanceName = Configuration["DataInstanceName"];
            var telegramConfig = Configuration.GetSection("Telegram").Get<TelegramConfiguration>();
            var botConfig = Configuration.GetSection("Bot").Get<BotConfiguration>();
            var sendgridConfig = Configuration.GetSection("SendGrid").Get<SendGridConfiguration>();
            var hCaptchaConfig = Configuration.GetSection("hCaptcha").Get<HCaptchaConfiguration>();

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
            services.AddSingleton<IUserRepository, UserRepository>();

            services.AddScoped<ITelegramBotService, TelegramBotService>();
            services.AddBotCortex();
            services.AddScoped<ICovidPassLinkerService, CovidPassLinkerService>();
            services.AddScoped<ICovidPassPollService, CovidPassPollService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IEmailService, EmailService>();

            // Caching solution for polls and passes
            var redisConfig = Configuration.GetConnectionString("Redis");
            var cosmosConfig = Configuration.GetConnectionString("Cosmos");

            if (!string.IsNullOrEmpty(redisConfig))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.InstanceName = dataInstanceName;
                    options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConfig);
                    options.ConfigurationOptions.CertificateValidation += ConfigurationOptions_CertificateValidation;
                });
            }
            else if (!string.IsNullOrEmpty(cosmosConfig))
            {
                services.AddCosmosCache(options =>
                {
                    options.DatabaseName = dataInstanceName;
                    options.ContainerName = "DistributedCache";
                    options.ClientBuilder = new Microsoft.Azure.Cosmos.Fluent.CosmosClientBuilder(cosmosConfig);
                    options.CreateIfNotExists = true;
                });
            }
            else
            {
                services.AddDistributedMemoryCache();
            }

            var mongoConfig = Configuration.GetConnectionString("Mongo");
            // For everything else.. there's mongo
            services.AddSingleton<IMongoDataSource, MongoDataSource>(sc =>
            {
                return new MongoDataSource(mongoConfig, dataInstanceName);
            });

            // Other 3rd party stuff
            services.AddHCaptcha(options =>
            {
                options.SiteKey = hCaptchaConfig.SiteKey;
                options.Secret = hCaptchaConfig.Secret;
            });
            services.AddSendGrid(options =>
            {
                options.ApiKey = sendgridConfig.ApiKey;
            });
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
            var cultureInfo = new CultureInfo("en-NZ");

            if (cultureInfo is not null)
            {
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
            }

            Log.Information("Culture set to {culture}", Thread.CurrentThread.CurrentCulture.DisplayName);

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
