﻿using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse{

    public interface IGpuInput : IComputeNode
    {
        void SetParameterValue(ParameterCollection theCollection);
        
        public void CheckDeclaration(bool theCallChangeEvent = true);
    }
    
    

    public class DefaultInput<T> : ShaderNode<T>, IGpuInput
    {
        public DefaultInput(NodeContext nodeContext, string theName) : base(nodeContext, theName, null)
        {
            SetInputs(new List<AbstractShaderNode>());
        }

        public void SetParameterValue(ParameterCollection theCollection)
        {
        }

        public void CheckDeclaration(bool theCallChangeEvent = true)
        {
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public override string ID => Name;
    }
    
    public class FieldDeclaration
    {
        private string _computeShaderDeclaration;
        private string _declaration;

        public FieldDeclaration(bool isResource, string computeShaderDeclaration, string declaration)
        {
            IsResource = isResource;
            _computeShaderDeclaration = computeShaderDeclaration;
            _declaration = declaration;
        }

        public FieldDeclaration(string declaration)
        {
            _computeShaderDeclaration = declaration;
            _declaration = declaration;
        }

        public void Set(string computeShaderDeclaration, string declaration)
        {
            _computeShaderDeclaration = computeShaderDeclaration;
            _declaration = declaration;
        }

        public string GetDeclaration(bool theIsComputeShader)
        {
            return theIsComputeShader && _computeShaderDeclaration != null ? _computeShaderDeclaration : _declaration;
        }

        public override string ToString()
        {
            return _declaration;
        }

        public bool IsResource { get; }
    }

    public abstract class AbstractInput<T, TParameterKeyType, TParameterUpdaterType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T> where TParameterUpdaterType : ParameterUpdater<T,TParameterKeyType>
     {
         
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

         protected readonly TParameterUpdaterType Updater;// = new ValueParameterUpdater<T>();

         protected T _value;

         private readonly FieldDeclaration _fieldDeclaration;
         
         protected AbstractInput(NodeContext nodeContext, string theName, bool theIsResource): base(nodeContext, theName)
         {
             Updater = CreateUpdater();
             SetInputs( new List<AbstractShaderNode>());
             AddProperty(Inputs, this);

             _fieldDeclaration = new FieldDeclaration(theIsResource, "", "");
             AddProperty(Declarations,_fieldDeclaration);
         }

         protected abstract TParameterUpdaterType CreateUpdater();

        

         protected override string SourceTemplate()
         {
             return "";
         }

         protected TParameterKeyType ParameterKey { get; set;}

         public virtual T Value
         { 
             get => Updater.Value;
             set
             {
                 _value = value;
                 Updater.Value = value;
             }
         }

         private string BuildDeclaration(string theTypeName)
         {
             return ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", ID},
                     {"inputType", theTypeName}
                 });
         }

         protected void SetFieldDeclaration(string theTypeName, string theComputeTypeName)
         {
             _fieldDeclaration.Set(BuildDeclaration(theComputeTypeName),BuildDeclaration(theTypeName));
         }

         protected void SetFieldDeclaration(string theTypeName)
         {
             SetFieldDeclaration(theTypeName,theTypeName);
         }
         
         public abstract void SetParameterValue(ParameterCollection theCollection);

         public abstract void CheckDeclaration(bool theCallChangeEvent = true);
     }

     public abstract class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>, ObjectParameterUpdater<T>> where T : class
     {
         protected ObjectInput(NodeContext nodeContext, string theName) : base(nodeContext, theName, true)
         {
             ParameterKey = new ObjectParameterKey<T>(ID);
         }

         protected override ObjectParameterUpdater<T> CreateUpdater()
         {
             return new ObjectParameterUpdater<T>();
         }
         
         protected override void OnPassContext(ShaderGeneratorContext nodeContext)
         {
             Updater.Track(nodeContext, ParameterKey);
             Updater.Value = _value;
         }
         
         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, _value);
         }

     }

     public abstract class ChangeableObjectInput<T> : ObjectInput<T> where T : class
     {
         private string _lastComputeDeclaration;
         private string _lastDeclaration;
         protected ChangeableObjectInput(NodeContext nodeContext, string theName) : base(nodeContext, theName)
         {
             _lastComputeDeclaration = "";
             _lastDeclaration = "";
         }

         public abstract string Declaration();

         public abstract string ComputeDeclaration();

         public override void CheckDeclaration(bool callChangeEvent = true)
         {
             var declaration = Declaration();
             var computeDeclaration = ComputeDeclaration();
             
             if (computeDeclaration.Equals(_lastComputeDeclaration) &&
                 declaration.Equals(_lastDeclaration)) return;

             _lastComputeDeclaration = computeDeclaration;
             _lastDeclaration = declaration;
             SetFieldDeclaration(computeDeclaration, declaration);

             if(callChangeEvent)CallChangeEvent();
         }
     }

     public class TextureInput : ChangeableObjectInput<Texture>
     {
         private bool _useRw;
         public TextureInput(NodeContext nodeContext) : base(nodeContext, "TextureInput")
         {
             _useRw = false;
         }

         public void SetInput(Texture theTexture, bool theUseRw, bool theCallChangeEvent = true)
         {
             Value = theTexture;
             _useRw = theUseRw;

             CheckDeclaration(theCallChangeEvent);
         }

         public override string Declaration()
         {
             if (Value == null)
             {
                 return "Texture1D";
             }

             return
                 Value.Dimension switch
                 {
                     TextureDimension.Texture1D => "Texture1D",
                     TextureDimension.Texture2D => "Texture2D",
                     TextureDimension.Texture3D => "Texture3D",
                     TextureDimension.TextureCube => "TextureCube",
                     _ => "Texture2D"
                 };

         }

         public override string ComputeDeclaration()
         {
             return Value == null ? "Texture1D" : TypeHelpers.TextureTypeName(Value, _useRw);
         }
     }
     
     public class SamplerInput: ObjectInput<SamplerState>, IGpuInput 
     {
         public SamplerInput(NodeContext nodeContext) : base(nodeContext, "SamplerInput")
         {
             SetFieldDeclaration("SamplerState", "SamplerState");
         }

         public override void CheckDeclaration(bool theCallChangeEvent = true)
         {
         }
     }

     public class BufferInput<T>: ChangeableObjectInput<Buffer>, IBufferInput<T>
     {
         private bool _append;

         private bool _forceAppendConsume = false;
         
         public ShaderNode<T> Type { get; private set; }

         public bool ForceAppendConsume { get=>_forceAppendConsume;
             set
             {
                 _forceAppendConsume = value; 
                 CheckDeclaration();
             }
         }

         public override string TypeName()
         {
             return Type == null ? TypeHelpers.GetGpuType<T>() : Type.TypeName();
         }

         public BufferInput(NodeContext nodeContext, ShaderNode<T> theType) : base(nodeContext, "DynamicBufferInput")
         {
             ForceAppendConsume = false;
             SetInput(null,   theType);
         }

         public bool Append { 
             get => _append;
             set
             {
                 _append = value; 
                 CheckDeclaration();
             }
         }

         public void SetInput(Buffer theBuffer, ShaderNode<T> theType)
         {
             Value = theBuffer;
             Type = theType;
             if(theType != null)SetInputs(new List<AbstractShaderNode>{theType}, false);
             CheckDeclaration();
         }

         public override string Declaration()
         {
             return TypeHelpers.BufferTypeName(Value, Type == null ? TypeHelpers.GetGpuType<T>() : Type.TypeName(), Append, false, ForceAppendConsume);
         }

         public override string ComputeDeclaration()
         {
             return Declaration();
         }
     }
     
     public class TypedBufferInput<T>: ChangeableObjectInput<Buffer<T>> where T : unmanaged
     {
         private bool _append;

         public bool ForceAppendConsume { get; set; }

         public TypedBufferInput(NodeContext nodeContext) : base(nodeContext, "BufferInput")
         {
             ForceAppendConsume = false;
             SetInput(null, true);
         }

         public void SetInput(Buffer<T> theBuffer,  bool theAppend)
         {
             Value = theBuffer;
             _append = theAppend;
             
             CheckDeclaration();
         }
         
         public override string Declaration()
         {
             return TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuType<T>(), _append, false, ForceAppendConsume);
         }

         public override string ComputeDeclaration()
         {
             return Declaration();
         }
     }
     
    public class ValueInput<T> : AbstractInput<T,ValueParameterKey<T>, ValueParameterUpdater<T>> where T : struct 
    {

        public ValueInput(NodeContext nodeContext): base(nodeContext, "input", false)
        {
            ParameterKey = new ValueParameterKey<T>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuType<T>());
        }

        protected override ValueParameterUpdater<T> CreateUpdater()
        {
            return new ValueParameterUpdater<T>();
        }

        protected override void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            try
            {
                Updater.Track(nodeContext, ParameterKey);
                Updater.Value = _value;
            }
            catch (Exception)
            {
               // Console.WriteLine(e);
            }
            
        }
        
        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }

        public override void CheckDeclaration(bool theCallChangeEvent = true)
        {
        }
    }
    /*
    public class CompositionInput<T> : AbstractInput<IComputeNode,PermutationParameterKey<T>, PermutationParameterUpdater<T>> where T : IComputeNode
    {

        public CompositionInput(SetVar<T> value): base("input", false, value)
        {
        }

        protected override PermutationParameterUpdater<IComputeNode> CreateUpdater()
        {
            return new PermutationParameterUpdater<IComputeNode>();
        }

        protected override void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            Updater.Track(nodeContext, ParameterKey);
        }

        protected override void OnGenerateCode(ShaderGeneratorContext nodeContext)
        {
            ParameterKey = new PermutationParameterKey<ShaderSource>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        
    }*/
}