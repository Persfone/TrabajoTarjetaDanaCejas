// Tiempo.cs
using System;

namespace ManejoDeTiempos
{
    public class Tiempo
    {
        public virtual DateTime Now()
        {
            return DateTime.Now;
        }
    }
}