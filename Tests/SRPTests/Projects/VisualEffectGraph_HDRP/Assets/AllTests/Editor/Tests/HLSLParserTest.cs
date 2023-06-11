#if !UNITY_EDITOR_OSX || MAC_FORCE_TESTS
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.VFX.Block;
using UnityEngine;

namespace UnityEditor.VFX.Test
{
    [TestFixture]
    class VFXCustomHLSLTest
    {
        const string templateHlslCode = "void CustomHLSL(inout VFXAttributes attributes, {0} {1} {2}) {{{3}}}";

        [TestCase("float", typeof(float))]
        [TestCase("uint", typeof(uint))]
        [TestCase("int", typeof(int))]
        [TestCase("bool", typeof(bool))]
        [TestCase("float2", typeof(Vector2))]
        [TestCase("float3", typeof(Vector3))]
        [TestCase("float4", typeof(Vector4))]
        [TestCase("float4x4", typeof(Matrix4x4))]
        [TestCase("VFXSampler2D", typeof(Texture2D))]
        [TestCase("VFXSampler3D", typeof(Texture3D))]
        [TestCase("VFXGradient", typeof(Gradient))]
        [TestCase("VFXCurve", typeof(AnimationCurve))]
        [TestCase("ByteAddressBuffer", typeof(GraphicsBuffer))]
        public void HLSL_Check_Parameter_Supported_Types(string hlslType, Type csharpType)
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, "in", hlslType, "param", string.Empty);

            // Act
            var functions = HLSLFunction.Parse(hlslCode).ToArray();
            var function = functions.FirstOrDefault();

            // Assert
            Assert.AreEqual(1, functions.Length, "HLSL code could not be parsed properly to detect a function");
            Assert.NotNull(function);

            Assert.AreEqual(2, function.inputs.Count(), "Function parameters were not properly detected");
            var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
            Assert.NotNull(input);
            Assert.AreEqual(HLSLAccess.IN, input.access, "Wrong parameter access modifier");
            Assert.AreEqual("param", input.name, "Wrong parameter name");
            Assert.AreEqual(hlslType, input.rawType, "Wrong parameter hlsl type");
            Assert.AreEqual(csharpType, input.type, "Wrong parameter csharp type");
            Assert.IsNull(input.errors, "There was errors when parsing parameters");
        }

        [TestCase("in", HLSLAccess.IN)]
        [TestCase("out", HLSLAccess.OUT)]
        [TestCase("", HLSLAccess.NONE)]
        [TestCase("inout", HLSLAccess.INOUT)]
        public void HLSL_Check_Parameter_Access_Modifier(string modifier, HLSLAccess access)
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, modifier, "float", "param", string.Empty);

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            if (access != HLSLAccess.NONE)
            {
                var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
                Assert.AreEqual(access, input.access, "Wrong parameter access modifier");
            }
            else
            {
                // Should handle this case
            }
        }

        [TestCase("toto")]
        [TestCase("someName")]
        [TestCase("_otherName")]
        public void HLSL_Check_Parameter_Name(string name)
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, "in", "float", name, string.Empty);

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
            Assert.AreEqual(name, input.name, "Parameter name not correctly parsed");
        }

        [TestCase("int")]
        [TestCase("uint")]
        [TestCase("float")]
        public void HLSL_Check_Parameter_StructuredBuffer(string templateType)
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, "in", $"StructuredBuffer<{templateType}>", "buffer", string.Empty);

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
            Assert.NotNull(input);
            Assert.AreEqual(HLSLAccess.IN, input.access, "Wrong parameter access modifier");
            Assert.AreEqual("buffer", input.name, "Wrong parameter name");
            Assert.AreEqual("StructuredBuffer", input.rawType, "Wrong parameter hlsl type");
            Assert.AreEqual(typeof(GraphicsBuffer), input.type, "Wrong parameter csharp type");
            Assert.AreEqual(templateType, input.templatedType, "Wrong Structured buffer template parameter type");
            Assert.IsNull(input.errors, "There was errors when parsing parameters");
        }

        [Test]
        public void HLSL_Check_Parameter_ByteAddressBuffer()
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, "in", $"ByteAddressBuffer", "buffer", string.Empty);

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
            Assert.NotNull(input);
            Assert.AreEqual("ByteAddressBuffer", input.rawType, "Wrong parameter hlsl type");
            Assert.AreEqual(typeof(GraphicsBuffer), input.type, "Wrong parameter csharp type");
            Assert.IsEmpty(input.templatedType, "ByteAddressBuffer must not have a template type");
            Assert.IsNull(input.errors, "There was errors when parsing parameters");
        }

        [Test]
        public void HLSL_Check_Parameter_Unsupported_Type()
        {
            // Arrange
            var hlslCode = string.Format(templateHlslCode, "in", $"float2x2", "mat", string.Empty);

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            var input = function.inputs.ElementAt(1); // The first parameter is a VFXAttributes
            Assert.NotNull(input);
            Assert.IsNotEmpty(input.errors, "float2x2 is not a supported type, the parameter should should hold an error");
            Assert.IsInstanceOf<HLSLUnknownParameterType>(input.errors.Single());
        }

        [Test]
        public void HLSL_Check_Parameter_Documentation_Type()
        {
            // Arrange
            var hlslCode =
                "/// offset: this is the offset" + "\n" +
                "/// speedFactor: this is the speedFactor" + "\n" +
                "void CustomHLSL(inout VFXAttributes attributes, in float3 offset, in float speedFactor)" + "\n" +
                "{" + "\n" +
                "  attributes.position += offset;" + "\n" +
                "  attributes.velocity *= speedFactor;" + "\n" +
                "}";

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            foreach (var parameter in function.inputs.Skip(1)) // Skip the first parameter which is VFXAttributes
            {
                Assert.AreEqual($"this is the {parameter.name}", parameter.tooltip);
            }
        }

        [Test]
        public void HLSL_Check_Hidden_Function()
        {
            // Arrange
            var hlslCode =
                "/// Hidden" + "\n" +
                "void HelperFunction(in float param, out float3 pos)" + "\n" +
                "{" + "\n" +
                "  float3 pos = float3(param, param, param);" + "\n" +
                "}";

            // Act
            var functions = HLSLFunction.Parse(hlslCode);

            // Assert
            Assert.IsEmpty(functions);
        }

        [Test]
        public void HLSL_Check_Parse_Include()
        {
            // Arrange
            var includePath = "/path/to/include/file.hlsl";

            var hlslCode =
                $"#include \"{includePath}\"" + "\n" +
                "void HelperFunction(in float param, out float3 pos)" + "\n" +
                "{" + "\n" +
                "  float3 pos = float3(param, param, param);" + "\n" +
                "}";

            // Act
            var includes = HLSLParser.ParseIncludes(hlslCode).ToArray();

            // Assert
            Assert.IsNotEmpty(includes);
            Assert.AreEqual(1, includes.Length);
            Assert.AreEqual(includePath, includes[0]);
        }

        [Test]
        public void HLSL_Check_Space_Before_Parameter()
        {
            // Arrange
            var hlslCode =
                $"void Function( in float param)" + "\n" +
                "{" + "\n" +
                "  float3 pos = float3(param, param, param);" + "\n" +
                "}";

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            var input = function.inputs.FirstOrDefault();
            Assert.NotNull(input, "Could not properly parse input parameter");
            Assert.AreEqual("param", input.name);
            Assert.AreEqual(HLSLAccess.IN, input.access);
            Assert.AreEqual(typeof(float), input.type);
            Assert.AreEqual("float", input.rawType);
        }

        [Test]
        public void HLSL_Check_Use_Of_Rand_Macro()
        {
            // Arrange
            var hlslCode =
                $"void Function(in VFXAttributes attributes)" + "\n" +
                "{" + "\n" +
                "  float r = VFXRAND;" + "\n" +
                "}";

            // Act
            var function = HLSLFunction.Parse(hlslCode).Single();

            // Assert
            Assert.AreEqual(1, function.attributes.Count);
            Assert.AreEqual("seed", function.attributes.Single().attrib.name);
        }

        [Test]
        public void HLSL_Check_Missing_Closing_Curly_Bracket()
        {
            // Arrange
            var hlslCode =
                $"void Function(in float param)" + "\n" +
                "{" + "\n" +
                "  float3 pos = float3(param, param, param);" + "\n";

            // Act
            var functions = HLSLFunction.Parse(hlslCode);

            // Assert
            CollectionAssert.IsEmpty(functions);
        }
    }
}
#endif
