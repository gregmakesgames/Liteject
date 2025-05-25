using System;

namespace Liteject
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class NoReflectionBakingAttribute : Attribute
    {
    }
}
