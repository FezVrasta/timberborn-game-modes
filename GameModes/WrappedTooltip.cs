using System;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace GameModes
{
    /**
     * A tooltip that wraps at a fixed width, so a long text doesn't run as wide as its words across the screen.
     */
    public static class WrappedTooltip
    {
        public const int Width = 420;

        public static void Register(ITooltipRegistrar registrar, VisualElement target, Func<string> text) => registrar.Register(target, () =>
        {
            var label = new Label(text());
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.width = Width;
            return label;
        });
    }
}
