#nullable enable
using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace LogikSpiel.Core;

public static class ViewAnimationExtensions
{
    public static Task TranslateToAsync(this VisualElement view, double x, double y, uint length, Easing? easing = null)
        => view.TranslateTo(x, y, length, easing);
}
