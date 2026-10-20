using System;

namespace StupidTemplate.Classes
{
    public sealed class ButtonCategory
    {
        public string       name      = "Unnamed";
        public bool         adminOnly = false;
        public ButtonInfo[] buttons   = [];
    }
}