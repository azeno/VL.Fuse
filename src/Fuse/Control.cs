﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class SwitchBoolean<T> : ShaderNode<T>
    {
        private readonly ShaderNode<bool> _inCheck;
        private readonly ShaderNode<T> _inFalse;
        private readonly ShaderNode<T> _inTrue;
        public SwitchBoolean(ShaderNode<bool> inCheck, ShaderNode<T> inFalse, ShaderNode<T> inTrue, ShaderNode<T> theDefault = null) : base( "expression", theDefault)
        {

            var ins = new List<AbstractShaderNode>(){inCheck};
            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            if(_inFalse != null)ins.Add(_inFalse);
            if(_inTrue != null)ins.Add(_inTrue);
            if(_inFalse == null && _inTrue == null)ins.Add(null);
            
            SetInputs(ins);
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${check} ? ${inTrue} : ${inFalse};", 
                new Dictionary<string, string>
                {
                    {"check", _inCheck.ID},
                    {"inFalse", _inFalse.ID},
                    {"inTrue", _inTrue.ID}
                });
        }
    }
    
    public class SwitchNumeric<T> : ShaderNode<T>
    {
        private new const string ShaderCode = @"
    ${resultType} ${resultName}; 
    switch(${check}){
${cases}
    }
        ";

        private readonly ShaderNode<T> _default;
        private readonly List<ShaderNode<T>> _cases;
        private readonly ShaderNode<int> _check;
        public SwitchNumeric(ShaderNode<int> theCheck, IEnumerable<ShaderNode<T>> inputs, ShaderNode<T> theDefault) : base("numericSwitch")
        {
            _check = theCheck;
            _cases = inputs.ToList();
            _default = theDefault;
            var myIns = new List<AbstractShaderNode>(_cases);
            if (theDefault != null)
            {
                myIns.Add(theDefault);
            }
            
            var myKeyMap = new Dictionary<string, string>
            {
                {"check", theCheck.ID}
            };
            SetInputs(myIns);
            
        }
        
        private static string BuildCases(IEnumerable<AbstractShaderNode> inputs, ShaderNode<T> theDefault)
        {
            var stringBuilder = new StringBuilder();
            var c = 0;
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append("    case " + c + ":"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + input.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" + Environment.NewLine);
                c++;
            });
            if (theDefault != null)
            {
                stringBuilder.Append("    default:"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + theDefault.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" );
            }
            return stringBuilder.ToString();
        }

        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  base.CreateTemplateMap();
            result["check"] = _check.ID;
            return result;
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"cases", BuildCases(_cases, _default)}});
        }
    }
    
    public class Not: ShaderNode<bool>
    {
        private readonly ShaderNode<bool> _in;

        public Not(ShaderNode<bool> theIn) : base("not")
        {
            _in = theIn ??  new ConstantValue<bool>(false);
            
            SetInputs( new List<AbstractShaderNode>{_in});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = !${in};", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                });
        }
    }
    
    public class SimpleKeyword : ShaderNode<GpuVoid>, IComputeVoid
    {

        private string _keyword;

        protected SimpleKeyword(string theKeyword) : base( "void")
        {
            _keyword = theKeyword;
            SetInputs(new List<AbstractShaderNode>{null});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return _keyword;
        }

        protected override string SourceTemplate()
        {
            return _keyword;
        }
    }
    
    public class EmptyVoid : SimpleKeyword
    {
    
        public EmptyVoid() : base( "")
        {
        }
    }
    
    public class ReturnVoid : SimpleKeyword
    {
    
        public ReturnVoid() : base( "return;")
        {
        }
    }
    
    public class BreakVoid : SimpleKeyword
    {
    
        public BreakVoid() : base( "break;")
        {
        }
    }
    
    public class ContinueVoid : SimpleKeyword
    {
    
        public ContinueVoid() : base( "continue;")
        {
        }
    }
}