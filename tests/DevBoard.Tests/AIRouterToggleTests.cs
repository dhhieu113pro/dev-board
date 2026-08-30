using System;
using System.Reflection;
using System.Threading.Tasks;

using DevBoard.ViewModels;

using Xunit;

namespace DevBoard.Tests;

public class AIRouterToggleTests
{
    [Fact]
    public void ViewModel_ExposesImmediateRouterToggle()
    {
        var viewModelType = typeof(DevSpaceAIRouter);

        var enabledProperty = viewModelType.GetProperty("IsEnabled", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(enabledProperty);
        Assert.Equal(typeof(bool), enabledProperty.PropertyType);
        Assert.True(enabledProperty.CanRead);

        var setEnabledMethod = viewModelType.GetMethod(
            "SetEnabledAsync",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(bool)],
            modifiers: null);
        Assert.NotNull(setEnabledMethod);
        Assert.Equal(typeof(Task), setEnabledMethod.ReturnType);
    }
}
