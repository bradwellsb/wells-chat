using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Controls;

namespace WellsChat.Maui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}