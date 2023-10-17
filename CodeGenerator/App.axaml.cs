using Avalonia.Controls;
using CodeGenerator.Views;
using Prism.DryIoc;
using Prism.Ioc;

namespace CodeGenerator;

public partial class App : PrismApplication
{
    protected override Window CreateShell()
    {
        return Container.Resolve<MainWindow>();
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        
    }
}