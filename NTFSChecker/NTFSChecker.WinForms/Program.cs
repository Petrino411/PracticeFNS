﻿using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NTFSChecker.WinForms.Services;

namespace NTFSChecker.WinForms;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; set; }

    /// <summary>
    /// Главная точка входа для приложения.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var services = ConfigureServices();
        ServiceProvider = services.BuildServiceProvider();

        var mainForm = ServiceProvider.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }

    private static IServiceCollection ConfigureServices()
    {
            
        var services = new ServiceCollection();
            
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddConsole();
            builder.AddDebug();
        });
            

        services.AddTransient<MainForm>();
        services.AddTransient<SettingsForm>();
        services.AddSingleton<ExcelWriter>();
        services.AddSingleton<UserGroupHelper>();
        services.AddSingleton<DirectoryChecker>();


        return services;
    }
       
}