using System;

namespace Vishcious.ArcGIS.SLContrib
{
    public interface IPropertyValue<T>
    {
        // Properties
        string Name
        {
            get;
            set;
        }
        T Value
        {
            get;
            set;
        }
    }
}