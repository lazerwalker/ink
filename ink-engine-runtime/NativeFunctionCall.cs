﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ink.Runtime
{
    internal class NativeFunctionCall : Runtime.Object
    {
        public const string Add      = "+";
        public const string Subtract = "-";
        public const string Divide   = "/";
        public const string Multiply = "*";
        public const string Mod      = "%";
        public const string Negate   = "~";

        public const string Equal    = "==";
        public const string Greater  = ">";
        public const string Less     = "<";
        public const string GreaterThanOrEquals = ">=";
        public const string LessThanOrEquals = "<=";
        public const string NotEquals   = "!=";
        public const string Not      = "!";

        public const string And      = "&&";
        public const string Or       = "||";

        public const string Min      = "MIN";
        public const string Max      = "MAX";

        public static NativeFunctionCall CallWithName(string functionName)
        {
            return new NativeFunctionCall (functionName);
        }

        [JsonProperty("f")]
        [UniqueJsonIdentifier]
        public string name { 
            get { 
                return _name;
            } 
            protected set {
                _name = value;
                if( !_isPrototype )
                    _prototype = _nativeFunctions [_name];
            }
        }
        string _name;

        public int numberOfParameters { 
            get {
                if (_prototype) {
                    return _prototype.numberOfParameters;
                } else {
                    return _numberOfParameters;
                }
            }
            protected set {
                _numberOfParameters = value;
            }
        }

        int _numberOfParameters;

        public Runtime.Object Call(List<Runtime.Object> parameters)
        {
            if (_prototype) {
                return _prototype.Call(parameters);
            }

            if (numberOfParameters != parameters.Count) {
                throw new System.Exception ("Unexpected number of parameters");
            }

            foreach (var p in parameters) {
                if (p is Void)
                    throw new StoryException ("Attempting to perform operation on a void value. Did you forget to 'return' a value from a function you called here?");
            }

            var coercedParams = CoerceLiteralsToSingleType (parameters);
            LiteralType coercedType = coercedParams[0].literalType;

            if (coercedType == LiteralType.Int) {
                return Call<int> (coercedParams);
            } else if (coercedType == LiteralType.Float) {
                return Call<float> (coercedParams);
            } else if (coercedType == LiteralType.String) {
                return Call<string> (coercedParams);
            } else if (coercedType == LiteralType.DivertTarget) {
                return Call<Path> (coercedParams);
            }

            return null;
        }

        Literal Call<T>(List<Literal> parametersOfSingleType)
        {
            Literal param1 = (Literal) parametersOfSingleType [0];
            LiteralType litType = param1.literalType;

            var val1 = (Literal<T>)param1;

            int paramCount = parametersOfSingleType.Count;

            if (paramCount == 2 || paramCount == 1) {

                object opForTypeObj = null;
                if (!_operationFuncs.TryGetValue (litType, out opForTypeObj)) {
                    throw new StoryException ("Can not perform operation '"+this.name+"' on "+litType);
                }

                // Binary
                if (paramCount == 2) {
                    Literal param2 = (Literal) parametersOfSingleType [1];

                    var val2 = (Literal<T>)param2;

                    var opForType = (BinaryOp<T>)opForTypeObj;

                    // Return value unknown until it's evaluated
                    object resultVal = opForType (val1.value, val2.value);

                    return Literal.Create (resultVal);
                } 

                // Unary
                else {

                    var opForType = (UnaryOp<T>)opForTypeObj;

                    var resultVal = opForType (val1.value);

                    return Literal.Create (resultVal);
                }  
            }
                
            else {
                throw new System.Exception ("Unexpected number of parameters to NativeFunctionCall: " + parametersOfSingleType.Count);
            }
        }

        List<Literal> CoerceLiteralsToSingleType(List<Runtime.Object> parametersIn)
        {
            LiteralType litType = LiteralType.Int;

            // Find out what the output type is
            // "higher level" types infect both so that binary operations
            // use the same type on both sides. e.g. binary operation of
            // int and float causes the int to be casted to a float.
            foreach (var obj in parametersIn) {
                var literal = (Literal)obj;
                if (literal.literalType > litType) {
                    litType = literal.literalType;
                }
            }

            // Coerce to this chosen type
            var parametersOut = new List<Literal> ();
            foreach (Literal literal in parametersIn) {
                var castedLiteral = literal.Cast (litType);
                parametersOut.Add (castedLiteral);
            }

            return parametersOut;
        }

        public NativeFunctionCall(string name)
        {
            GenerateNativeFunctionsIfNecessary ();

            this.name = name;
        }

        // Require default constructor for serialisation
        public NativeFunctionCall() { 
            GenerateNativeFunctionsIfNecessary ();
        }

        // Only called internally to generate prototypes
        NativeFunctionCall (string name, int numberOfParamters)
        {
            _isPrototype = true;
            this.name = name;
            this.numberOfParameters = numberOfParamters;
        }
            
        static void GenerateNativeFunctionsIfNecessary()
        {
            if (_nativeFunctions == null) {
                _nativeFunctions = new Dictionary<string, NativeFunctionCall> ();

                // Int operations
                AddIntBinaryOp(Add,      (x, y) => x + y);
                AddIntBinaryOp(Subtract, (x, y) => x - y);
                AddIntBinaryOp(Multiply, (x, y) => x * y);
                AddIntBinaryOp(Divide,   (x, y) => x / y);
                AddIntBinaryOp(Mod,      (x, y) => x % y); 
                AddIntUnaryOp (Negate,   x => -x); 

                AddIntBinaryOp(Equal,    (x, y) => x == y ? 1 : 0);
                AddIntBinaryOp(Greater,  (x, y) => x > y  ? 1 : 0);
                AddIntBinaryOp(Less,     (x, y) => x < y  ? 1 : 0);
                AddIntBinaryOp(GreaterThanOrEquals, (x, y) => x >= y ? 1 : 0);
                AddIntBinaryOp(LessThanOrEquals, (x, y) => x <= y ? 1 : 0);
                AddIntBinaryOp(NotEquals, (x, y) => x != y ? 1 : 0);
                AddIntUnaryOp (Not,       x => (x == 0) ? 1 : 0); 

                AddIntBinaryOp(And,      (x, y) => x != 0 && y != 0 ? 1 : 0);
                AddIntBinaryOp(Or,       (x, y) => x != 0 || y != 0 ? 1 : 0);

                AddIntBinaryOp(Max,      (x, y) => Math.Max(x, y));
                AddIntBinaryOp(Min,      (x, y) => Math.Min(x, y));

                // Float operations
                AddFloatBinaryOp(Add,      (x, y) => x + y);
                AddFloatBinaryOp(Subtract, (x, y) => x - y);
                AddFloatBinaryOp(Multiply, (x, y) => x * y);
                AddFloatBinaryOp(Divide,   (x, y) => x / y);
                AddFloatBinaryOp(Mod,      (x, y) => x % y); // TODO: Is this the operation we want for floats?
                AddFloatUnaryOp (Negate,   x => -x); 

                AddFloatBinaryOp(Equal,    (x, y) => x == y ? (int)1 : (int)0);
                AddFloatBinaryOp(Greater,  (x, y) => x > y  ? (int)1 : (int)0);
                AddFloatBinaryOp(Less,     (x, y) => x < y  ? (int)1 : (int)0);
                AddFloatBinaryOp(GreaterThanOrEquals, (x, y) => x >= y ? (int)1 : (int)0);
                AddFloatBinaryOp(LessThanOrEquals, (x, y) => x <= y ? (int)1 : (int)0);
                AddFloatBinaryOp(NotEquals, (x, y) => x != y ? (int)1 : (int)0);
                AddFloatUnaryOp (Not,       x => (x == 0.0f) ? (int)1 : (int)0); 

                AddFloatBinaryOp(And,      (x, y) => x != 0.0f && y != 0.0f ? (int)1 : (int)0);
                AddFloatBinaryOp(Or,       (x, y) => x != 0.0f || y != 0.0f ? (int)1 : (int)0);

                AddFloatBinaryOp(Max,      (x, y) => Math.Max(x, y));
                AddFloatBinaryOp(Min,      (x, y) => Math.Min(x, y));

                // String operations
                AddStringBinaryOp(Add,     (x, y) => x + y); // concat
                AddStringBinaryOp(Equal,   (x, y) => x.Equals(y) ? (int)1 : (int)0);

                // Special case: The only operation you can do on divert target literals
                BinaryOp<Path> divertTargetsEqual = (Path d1, Path d2) => {
                    return d1.Equals(d2) ? 1 : 0;
                };
                AddOpToNativeFunc (Equal, 2, LiteralType.DivertTarget, divertTargetsEqual);
            }
        }

        void AddOpFuncForType(LiteralType litType, object op)
        {
            if (_operationFuncs == null) {
                _operationFuncs = new Dictionary<LiteralType, object> ();
            }

            _operationFuncs [litType] = op;
        }

        static void AddOpToNativeFunc(string name, int args, LiteralType litType, object op)
        {
            NativeFunctionCall nativeFunc = null;
            if (!_nativeFunctions.TryGetValue (name, out nativeFunc)) {
                nativeFunc = new NativeFunctionCall (name, args);
                _nativeFunctions [name] = nativeFunc;
            }

            nativeFunc.AddOpFuncForType (litType, op);
        }

        static void AddIntBinaryOp(string name, BinaryOp<int> op)
        {
            AddOpToNativeFunc (name, 2, LiteralType.Int, op);
        }

        static void AddIntUnaryOp(string name, UnaryOp<int> op)
        {
            AddOpToNativeFunc (name, 1, LiteralType.Int, op);
        }

        static void AddFloatBinaryOp(string name, BinaryOp<float> op)
        {
            AddOpToNativeFunc (name, 2, LiteralType.Float, op);
        }

        static void AddStringBinaryOp(string name, BinaryOp<string> op)
        {
            AddOpToNativeFunc (name, 2, LiteralType.String, op);
        }

        static void AddFloatUnaryOp(string name, UnaryOp<int> op)
        {
            AddOpToNativeFunc (name, 1, LiteralType.Int, op);
        }

        public override string ToString ()
        {
            return "Native '" + name + "'";
        }

        delegate object BinaryOp<T>(T left, T right);
        delegate object UnaryOp<T>(T val);

        NativeFunctionCall _prototype;
        bool _isPrototype;

        // Operations for each data type, for a single operation (e.g. "+")
        Dictionary<LiteralType, object> _operationFuncs;

        static Dictionary<string, NativeFunctionCall> _nativeFunctions;

    }
}

