using dFakto.States.Workers.Config;
using dFakto.States.Workers.FileStores;
using dFakto.States.Workers.Sql;
using dFakto.States.Workers.Sql.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace dFakto.States.Workers.TestsHost
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddStepFunctions(
                Configuration.GetSection("stepFunctions").Get<StepFunctionsConfig>(),
            Configuration.GetSection("fileStores").Get<FileStoreFactoryConfig>(), x =>
            {
                if (string.IsNullOrWhiteSpace(x.Config.EnvironmentName))
                {
                    x.Config.EnvironmentName = _env.EnvironmentName;
                }
                
                x.AddFtpFileStore();
                x.AddDirectoryFileStore();

                x.AddSqlWorkers(Configuration.GetSection("databases")
                    .Get<IEnumerable<DatabaseConfig>>());

                //x.AddWorkers(Assembly.GetExecutingAssembly());
                x.AddWorker<HttpWorker>();
                x.AddWorker<GZipWorker>();
                x.AddWorker("Dummy", Task.FromResult);
                x.AddWorker("Hello", s => Task.FromResult($"Hello {s}!"));
            });

            services.AddRazorPages();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(e => e.MapRazorPages());
        }
    }
}