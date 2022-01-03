using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PathFinding.Persistence;
using PathFinding.ViewModels;

namespace PathFinding
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();

            this.InitializeComponent();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IStatePersistence, StatePersistence>();
            services.AddSingleton<MainWindowViewModel, MainWindowViewModel>();
            return services.BuildServiceProvider();
        }
    }
}
