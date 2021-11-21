// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mvc.RenderViewToString
{
    public class Program
    {
        public static async Task Main()
        {
            var sp = InitializeServices();

            await RenderViewAsync(sp);

            // without fix
            // Mem0: 127,16015625
            // Mem1: 312,8046875

            // with fix
            //Mem0: 126,984375
            //Mem1: 113,6640625

            Console.ReadLine();
        }

        static double GetMemMB => (double)Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

        public static IServiceProvider InitializeServices()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((hbc, services) => ConfigureServices(hbc, services))
                .Build();

            return host.Services;
        }

        public static async Task RenderViewAsync(IServiceProvider sp)
        {
            var model = new EmailViewModel
            {
                UserName = "User",
                SenderName = "Sender",
                UserData1 = 1,
                UserData2 = 2
            };

            /// warming up
            using (var serviceScope = sp.CreateScope())
            {
                var helper = serviceScope.ServiceProvider.GetRequiredService<RazorViewToStringRenderer>();
                await helper.RenderViewToStringAsync("Views/EmailTemplate.cshtml", model);
            }

            using (var serviceScope = sp.CreateScope())
            {
                var helper = serviceScope.ServiceProvider.GetRequiredService<RazorViewToStringRenderer>();

                Console.WriteLine("Mem0: " + GetMemMB);

                for (var i = 0; i < 500_000; i++)
                {
                    await helper.RenderViewToStringAsync("Views/EmailTemplate.cshtml", model);
                }

                Console.WriteLine("Mem1: " + GetMemMB);
            }
        }

        private static void ConfigureServices(HostBuilderContext hbc, IServiceCollection services)
        {
            var env = hbc.HostingEnvironment;

            services.TryAddSingleton<IWebHostEnvironment>(CustomWebHostEnvironment.CreateInstance(env));
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IHostingEnvironment>(LegacyHostingEnvironment.CreateInstance(env));
            services.AddControllersWithViews()
                .AddRazorRuntimeCompilation(options => options.FileProviders.Add(
                    new PhysicalFileProvider(env.ContentRootPath)));

            var diagnosticSource = new DiagnosticListener("Microsoft.AspNetCore");
            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<DiagnosticSource>(diagnosticSource);
            services.AddSingleton<DiagnosticListener>(diagnosticSource);
            services.AddLogging();
            services.AddMvc();
            services.AddTransient<RazorViewToStringRenderer>();

            services.AddSingleton<EmailReportGenerator>();

        }

        public class LegacyHostingEnvironment : Microsoft.AspNetCore.Hosting.IHostingEnvironment
        {
            public LegacyHostingEnvironment() { }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }

            public static LegacyHostingEnvironment
                CreateInstance(IHostEnvironment hostEnv)
            {
                var _hostingEnvironment = new LegacyHostingEnvironment
                {
                    EnvironmentName = hostEnv.EnvironmentName,
                    ApplicationName = hostEnv.ApplicationName,

                    ContentRootPath = hostEnv.ContentRootPath,
                    WebRootPath = hostEnv.ContentRootPath
                };

                _hostingEnvironment.ContentRootFileProvider =
                    new PhysicalFileProvider(_hostingEnvironment.ContentRootPath);

                _hostingEnvironment.WebRootFileProvider =
                    new PhysicalFileProvider(_hostingEnvironment.WebRootPath);

                return _hostingEnvironment;
            }
        }

        public class CustomWebHostEnvironment
        : IWebHostEnvironment
        {
            public CustomWebHostEnvironment()
            {
            }

            public string EnvironmentName { get; set; }
            public string ApplicationName { get; set; }
            public string WebRootPath { get; set; }
            public IFileProvider WebRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }

            public static CustomWebHostEnvironment
                CreateInstance(IHostEnvironment hostEnv)
            {
                var _webHostEnvironment = new CustomWebHostEnvironment
                {
                    EnvironmentName = hostEnv.EnvironmentName,
                    ApplicationName = hostEnv.ApplicationName,

                    ContentRootPath = hostEnv.ContentRootPath,
                    WebRootPath = hostEnv.ContentRootPath
                };

                _webHostEnvironment.ContentRootFileProvider =
                    new PhysicalFileProvider(_webHostEnvironment.ContentRootPath);

                _webHostEnvironment.WebRootFileProvider =
                    new PhysicalFileProvider(_webHostEnvironment.WebRootPath);

                return _webHostEnvironment;
            }
        }
    }
}
