using System;
using System.Drawing;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK;
using ImGuiNET;
using System.Runtime.CompilerServices;

namespace FluidSimulation
{
    public class Simulation : GameWindow
    {
        struct TestNode // making the node struct
        {
            public Vector4 color; // color of the node
            public Vector2 velocity; // velocity of the node
            public Vector2i position;
            public uint initialized; // if the nodes data is initialized
            public float density; // density of the node
            public float p;
            public float dummy;
        };

        public Simulation() : base(GameWindowSettings.Default, NativeWindowSettings.Default)
        {
            Size = new Vector2i(screenWidth, screenHeight);
            Title = "Fluid Simulation by Adi Zahavi";
            WindowBorder = WindowBorder.Fixed;
        }

        // starts from here
        float[] verts = {
            //Position          Texture coordinates
            1.0f,  1.0f, 0.0f, 1.0f, 1.0f, // top right
            1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // bottom left
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f  // top left
        };

        uint[] indices = {
            0, 1, 3, // first triangle
            1, 2, 3 // second triangle
        };

        int shaderProgramID = 0;
        int computeShaderProgramID = 0;
        int vao; // verts array object
        int vertices;
        int ebo; // element buffer object

        int textureID;

        int readSSBO;
        int writeSSBO;

        // inputs
        float timeModifier = 1;
        int paintSize = 5;
        float paintStrength = 0.1f;
        int mouseState = 0;
        int simulationPercision = 10;
        System.Numerics.Vector3 paintColor = System.Numerics.Vector3.Zero;

        ImGuiController controller;

        const int screenWidth = 1024;
        const int screenHeight = 768;
        const int imageWidth = screenWidth / 4;
        const int imageHeight = screenHeight / 4;

        // frame bool
        bool isFrame1 = true;

        protected override void OnLoad()
        {
            controller = new ImGuiController(screenWidth, screenHeight);

            GL.ClearColor(rgbToNormalized(53), rgbToNormalized(117), rgbToNormalized(40), 1.0f);
            GL.Viewport(0, 0, screenWidth, screenHeight);

            // create and set texture to whole screen
            vao = GL.GenVertexArray();
            vertices = GL.GenBuffer();
            ebo = GL.GenBuffer();

            // shader program
            shaderProgramID = LoadShaderProgram("../../../shader.vert", "../../../shader.frag");
            computeShaderProgramID = LoadComputeShaderProgram("../../../grid.comp");

            ////////

            readSSBO = GL.GenBuffer();
            writeSSBO = GL.GenBuffer();
            TestNode[] grid = new TestNode[imageWidth * imageHeight];

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, writeSSBO);
            GL.NamedBufferStorage(writeSSBO, Unsafe.SizeOf<TestNode>() * grid.Length, grid, BufferStorageFlags.DynamicStorageBit);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, writeSSBO); // readonly

            GL.BindBuffer(BufferTarget.CopyWriteBuffer, readSSBO);
            GL.NamedBufferStorage(readSSBO, Unsafe.SizeOf<TestNode>() * grid.Length, grid, BufferStorageFlags.DynamicStorageBit);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, readSSBO); // writeonly

            ////////

            // compute shader
            UpdateImage();

            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vertices);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float), verts, BufferUsageHint.DynamicDraw);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.DynamicDraw);

            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            base.OnLoad();
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            controller.Update(this, (float)args.Time);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            // use the compute shader before drawing the triangles
            GL.UseProgram(computeShaderProgramID);

            GL.ProgramUniform2(computeShaderProgramID, 9, MousePosition.X, MousePosition.Y);

            int rightClicking = 0;
            if (IsMouseButtonDown(MouseButton.Right)) rightClicking = 1;
            GL.ProgramUniform1(computeShaderProgramID, 8, rightClicking);
            GL.ProgramUniform1(computeShaderProgramID, 7, mouseState);
            GL.ProgramUniform1(computeShaderProgramID, 5, paintSize);
            GL.ProgramUniform1(computeShaderProgramID, 4, paintStrength);
            GL.ProgramUniform3(computeShaderProgramID, 3, paintColor.X, paintColor.Y, paintColor.Z);

            float deltaTime = (float)args.Time * timeModifier;
            GL.ProgramUniform1(computeShaderProgramID, 10, deltaTime);
            GL.ProgramUniform1(computeShaderProgramID, 11, simulationPercision);
            if(isFrame1)
            {
                isFrame1 = false;
                GL.ShaderStorageBlockBinding(computeShaderProgramID, 1, 2);
                GL.ShaderStorageBlockBinding(computeShaderProgramID, 0, 3);
            }
            else
            {
                isFrame1 = true;
                GL.ShaderStorageBlockBinding(computeShaderProgramID, 1, 3); // write
                GL.ShaderStorageBlockBinding(computeShaderProgramID, 0, 2); // read
                GL.ProgramUniform2(computeShaderProgramID, 6, MousePosition.X, MousePosition.Y);
            }

            GL.DispatchCompute(imageWidth / 8, imageHeight / 8, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureUpdateBarrierBit);
            GL.MemoryBarrier(MemoryBarrierFlags.BufferUpdateBarrierBit);

            // draw 2 triangles that from a square
            GL.UseProgram(shaderProgramID);
            GL.Uniform1(GL.GetUniformLocation(shaderProgramID, "tex"), 0);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureID);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            /////
            // Gui editor

            ImGui.Begin("Modifier");

            ImGui.SliderFloat("Time Scale", ref timeModifier, -10f, 10f);
            ImGui.SliderInt("Simulation Percision", ref simulationPercision, 1, 100);
            ImGui.Spacing();
            ImGui.SliderInt("Paint Size", ref paintSize, 1, 100);
            ImGui.SliderFloat("Paint Strength", ref paintStrength, -1.0f, 1.0f);
            ImGui.Spacing();
            if(ImGui.Button("Draw Density"))
            {
                mouseState = 0;
            }
            ImGui.SameLine();
            if (ImGui.Button("Draw Velocity"))
            {
                mouseState = 1;
            }
            ImGui.SameLine();
            if (ImGui.Button("Draw Color"))
            {
                mouseState = 2;
            }
            ImGui.Spacing();
            ImGui.ColorPicker3("Draw Color", ref paintColor);

            ImGui.End();
            /////
            controller.Render();
            ImGuiController.CheckGLError("End of frame");

            Context.SwapBuffers();
            base.OnRenderFrame(args);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);

            controller.PressChar((char)e.Key);
        }

        private void UpdateImage()
        {
            textureID = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureID);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            var pixels = new List<byte>(3 * imageWidth * imageHeight);
            Color c = Color.White;
            for (int y = 0; y < screenHeight; y++)
            {
                for (int x = 0; x < screenWidth; x++)
                {
                    pixels.Add(c.R);
                    pixels.Add(c.G);
                    pixels.Add(c.B);
                    pixels.Add(c.A);
                }
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, imageWidth, imageHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToArray());
            GL.BindImageTexture(0, textureID, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        }

        protected override void OnUnload()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vertices);
            GL.DeleteTexture(textureID);
            
            GL.BindBuffer(BufferTarget.CopyReadBuffer, 0);
            GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0);
            GL.DeleteBuffer(writeSSBO);
            GL.DeleteBuffer(readSSBO);

            GL.UseProgram(0);
            GL.DeleteProgram(shaderProgramID);
            GL.DeleteProgram(computeShaderProgramID);

            base.OnUnload();
        }

        private int LoadShader(string shaderLocation, ShaderType shaderType)
        {
            int shaderID = GL.CreateShader(shaderType);
            GL.ShaderSource(shaderID, File.ReadAllText(shaderLocation));
            GL.CompileShader(shaderID);
            string infoLog = GL.GetShaderInfoLog(shaderID);
            if(!string.IsNullOrEmpty(infoLog))
            {
                throw new Exception(infoLog);
            }
            return shaderID;
        }

        private int LoadShaderProgram(string vertShaderLocation, string fragShaderLocation)
        {
            int shaderProgramID = GL.CreateProgram();

            int vertShaderID = LoadShader(vertShaderLocation, ShaderType.VertexShader);
            int fragShaderID = LoadShader(fragShaderLocation, ShaderType.FragmentShader);
            
            GL.AttachShader(shaderProgramID, vertShaderID);
            GL.AttachShader(shaderProgramID, fragShaderID);

            GL.LinkProgram(shaderProgramID);

            GL.DetachShader(shaderProgramID, vertShaderID);
            GL.DetachShader(shaderProgramID, fragShaderID);

            GL.DeleteShader(vertShaderID);
            GL.DeleteShader(fragShaderID);

            string infoLog = GL.GetProgramInfoLog(shaderProgramID);
            if (!string.IsNullOrEmpty(infoLog))
            {
                throw new Exception(infoLog);
            }

            return shaderProgramID;
        }

        private int LoadComputeShaderProgram(string computeShaderLocation)
        {
            int computeProgramID = GL.CreateProgram();

            int computeShaderID = LoadShader(computeShaderLocation, ShaderType.ComputeShader);

            GL.AttachShader(computeProgramID, computeShaderID);

            GL.LinkProgram(computeProgramID);

            GL.DetachShader(shaderProgramID, computeShaderID);
            GL.DeleteShader(computeShaderID);

            string infoLog = GL.GetProgramInfoLog(computeProgramID);
            if (!string.IsNullOrEmpty(infoLog))
            {
                throw new Exception(infoLog);
            }

            return computeProgramID;
        }

        // utils
        private float rgbToNormalized(float i)
        {
            return i / 255;
        }
    }
}
