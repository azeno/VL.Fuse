﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{

    public interface IFunctionParameter : IComputeNode
    {
        string TypeName();

        string ID { get; }

        int ArgumentNumber { get; }
        uint HashCode { get; set; }
    }

    public enum InputModifier
    {
        In,
        Out,
        InOut
    }
    
    public class FunctionParameter<T> : ShaderNode<T> , IFunctionParameter
    {

        public FunctionParameter(NodeContext nodeContext, ShaderNode<T> theType, int theId = 0): base(nodeContext, "delegate")
        {
            Ins = new List<AbstractShaderNode>();
            ArgumentNumber = theId;
        }
        
        public override string ID => Name;
        
        public int ArgumentNumber { get; }

        public override string TypeName()
        {
            return TypeHelpers.GetGpuType<T>();
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public interface IDelegate
    {
        string Name { get; }
        string FunctionName { get; }
        
        IDictionary<string, string> Functions { get; }
        
        Dictionary<string, IList> PropertiesForTree();

        public void CheckContext(ShaderGeneratorContext theContext);
    }

    public class Delegate<T> :  ShaderNode<T>, IDelegate
    {
        public Delegate(NodeContext nodeContext, string theId = "Function", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
        }
        
        private static string BuildArguments(List<IFunctionParameter> inputs)
        {
            inputs.Sort((a,b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));
            var usedIDs = new HashSet<string>();
            
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (usedIDs.Contains(input.ID)) return;
                usedIDs.Add(input.ID);
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(' ');
                stringBuilder.Append(ShaderNodesUtil.FixName(input.ID));
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public void SetInput(AbstractShaderNode theDelegate)
        {
            
            if (theDelegate == null) return;
            Functions.Clear();
            Property.Clear();
            var functionParameters = theDelegate.FunctionParameters();
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuType<T>()},
                {"functionName", FunctionName},
                {"arguments", BuildArguments(functionParameters)},
                {"functionImplementation", theDelegate.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(FunctionName, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            theDelegate.PropertiesForTree().ForEach(kv =>
            {
                AddProperties(kv.Key, kv.Value );
            });
        }
        public string FunctionName => Name + "_" + HashCode;
    }

    public class Invoke<T> : ShaderNode<T>
    {
        private Delegate<T> _delegate;
        public Invoke(NodeContext nodeContext, string theId = "Invoke", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
            Functions = new Dictionary<string, string>();
            
            Name = theId;
        }

        public void SetInputs(Delegate<T> theDelegate, IEnumerable<AbstractShaderNode> theParameters)
        {
            _delegate?.Outs.Remove(this);
            _delegate = theDelegate;

            if (theDelegate == null)
            {
                CallChangeEvent();
                return;
            }
            _delegate?.Outs.Add(this);

            SetInputs(theParameters, false);
            
            CallChangeEvent();
        }

        public override void CheckContext(ShaderGeneratorContext theContext)
        {
            _delegate?.CheckContext(theContext);
            base.CheckContext(theContext);
        }

        protected override string SourceTemplate()
        {
            if (_delegate == null)
            {
                return GenerateDefaultSource();
            }
            const string shader = "${resultType} ${resultName} = ${functionName}(${arguments});";

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"functionName", _delegate.FunctionName}
                });
        }

        public sealed override IDictionary<string, string> Functions { get; protected set; }

    }
}