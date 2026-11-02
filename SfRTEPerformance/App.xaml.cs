using Microsoft.Extensions.DependencyInjection;
using Syncfusion.Licensing;

namespace SfRTEPerformance
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SyncfusionLicenseProvider.RegisterLicense("YOUR LICENSE KEY");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}