using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace FSharpWeekly.UWP
{
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
            LoadApplication(new FSharpWeekly.App());
        }
    }
}
