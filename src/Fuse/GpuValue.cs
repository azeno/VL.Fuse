﻿namespace Fuse
{
    public abstract class AbstractGpuValue
    {
        public  string Name{ get; set; }

        protected AbstractGpuValue(string theName)
        {
            Name = theName;
        }

        public abstract string ID { get; }

        public abstract string TypeName(); 

        public AbstractShaderNode ParentNode { get; set; }
    }

    public class GpuValue<T> : AbstractGpuValue
    {

        public string TypeOverride { get; set; }

        public GpuValue(string theName) : base(theName)
        {
        }

        public override string TypeName()
        {
            if (typeof(T) == typeof(GpuStruct))
            {
                return TypeOverride;
            }
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public override string ID => Name + "_" + GetHashCode();

    }

    public class GpuValuePassThrough<T> : GpuValue<T>
    {
       
        private GpuValue<T> _value;
        public GpuValuePassThrough(GpuValue<T> theValue) : base(theValue.Name)
        {
            _value = theValue;
        }
        public override string ID => _value.ID;
    }

    public class GpuNumericValue<T> : GpuValue<T> where T : struct
    {
        public GpuNumericValue(string theName) : base(theName)
        {
        }
    }
}