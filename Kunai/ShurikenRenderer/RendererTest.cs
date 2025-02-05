using System.IO;
using OpenTK.Graphics.OpenGL;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;
using Kunai.ShurikenRenderer;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;



namespace Shuriken.Rendering
{
    public class Renderer
    {
        public readonly string ShadersDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Shaders");

        private uint _vao;
        private uint _vbo;
        private uint _ebo;
        private uint[] _indices;
        public Dictionary<string, ShaderProgram> ShaderDictionary;

        private Vertex[] _buffer;
        private List<Quad> _quads;

        private bool _additive;
        private bool _linearFiltering = true;
        private int _textureId = -1;
        private ShaderProgram _shader;

        public readonly int MaxVertices = 10000;
        public int MaxQuads => MaxVertices / 4;
        public int MaxIndices => MaxQuads * 6;

        public int NumVertices { get; private set; }
        public int NumQuads => _quads.Count;
        public int NumIndices { get; private set; }
        public int BufferPos { get; private set; }
        public bool BatchStarted { get; private set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public bool Additive
        {
            get => _additive;
            set
            {
                _additive = value;
                if(_additive)
                {
                    GL.BlendFuncSeparate(BlendingFactorSrc.SrcColor, BlendingFactorDest.DstColor,
                      BlendingFactorSrc.SrcAlpha, BlendingFactorDest.DstAlpha);

                }
                else
                {

                }
                GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, (_additive ? BlendingFactorDest.One : BlendingFactorDest.OneMinusSrcAlpha),
                      BlendingFactorSrc.SrcAlpha, BlendingFactorDest.DstAlpha);
                //GL.BlendFunc(BlendingFactor.SrcAlpha, _additive ? BlendingFactor.One : BlendingFactor.OneMinusSrcAlpha);
            }
        }

        public bool LinearFiltering
        {
            get => _linearFiltering;
            set
            {
                _linearFiltering = value;

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    _linearFiltering ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    _linearFiltering ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            }
        }


        public int TextureId
        {
            get => _textureId;
            set
            {
                _textureId = value;
                _shader.SetBool("hasTexture", _textureId != -1);
            }
        }

        public Renderer(int in_Width, int in_Height)
        {
            ShaderDictionary = new Dictionary<string, ShaderProgram>();

            ShaderProgram basicShader = new ShaderProgram("basic", Path.Combine(ShadersDir, "basic.vert"), Path.Combine(ShadersDir, "basic.frag"));
            ShaderDictionary.Add(basicShader.Name, basicShader);

            // setup vertex indices
            _indices = new uint[MaxIndices];
            uint offset = 0;
            for (uint index = 0; index < MaxIndices; index += 6)
            {
                _indices[index + 0] = offset + 0;
                _indices[index + 1] = offset + 1;
                _indices[index + 2] = offset + 2;

                _indices[index + 3] = offset + 1;
                _indices[index + 4] = offset + 2;
                _indices[index + 5] = offset + 3;

                offset += 4;
            }

            _buffer = new Vertex[MaxVertices];
            _quads = new List<Quad>(MaxQuads);
            Init();

            Width = in_Width;
            Height = in_Height;
        }
        public List<Quad> GetQuads()
        {
            return _quads;
        }

        private void Init()
        {
            // 2 floats for pos, 2 floats for UVs, 4 floats for color
            int stride = Unsafe.SizeOf<Vertex>();

            GL.GenVertexArrays(1, out _vao);
            GL.BindVertexArray(_vao);

            GL.GenBuffers(1, out _vbo);
            GL.GenBuffers(1, out _ebo);

            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, MaxVertices, nint.Zero, BufferUsageHint.DynamicDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, MaxIndices, _indices, BufferUsageHint.StaticDraw);

            // position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

            // uv
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

            // color
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// Resets the number of quads, vertices, and indices.
        /// </summary>
        private void ResetRenderStats()
        {
            NumIndices = 0;
            NumVertices = 0;
        }

        /// <summary>
        /// Starts a new rendering batch.
        /// </summary>
        public void BeginBatch()
        {
            BufferPos = 0;
            BatchStarted = true;

            ResetRenderStats();
        }

        /// <summary>
        /// Ends the current rendering batch and flushes the vertex buffer
        /// </summary>
        public void EndBatch()
        {
            if (BufferPos > 0)
            {
                GL.BindVertexArray(_vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                GL.BufferSubData(BufferTarget.ArrayBuffer, nint.Zero, BufferPos * Unsafe.SizeOf<Vertex>(), _buffer);
                Flush();
            }

            BatchStarted = false;
        }

        private void Flush()
        {
            GL.DrawElements(PrimitiveType.Triangles, NumIndices, DrawElementsType.UnsignedInt, 0);
        }

        /// <summary>
        /// Pushes the quad parameters onto the vertex buffer.
        /// </summary>
        /// <param name="q">The quad to push to the buffer.</param>
        public void PushQuad(Quad in_Q)
        {
            /// SharpNeedle uses ARGB color, this inverts it so that colors look right
            _buffer[BufferPos++] = in_Q.TopLeft.WithInvertedColor();
            _buffer[BufferPos++] = in_Q.BottomLeft.WithInvertedColor();
            _buffer[BufferPos++] = in_Q.TopRight.WithInvertedColor();
            _buffer[BufferPos++] = in_Q.BottomRight.WithInvertedColor();
            NumIndices += 6;
        }

        public void DrawSprite(
            Vector2 in_TopLeft, Vector2 in_BottomLeft, Vector2 in_TopRight, Vector2 in_BottomRight,
            Vector2 in_Position, float in_Rotation, Vector2 in_Scale, float in_AspectRatio,
            Sprite in_Sprite, Sprite in_NextSprite, float in_SpriteFactor, Vector4 in_Color,
            Vector4 in_GradientTopLeft, Vector4 in_GradientBottomLeft, Vector4 in_GradientTopRight, Vector4 in_GradientBottomRight,
            int in_ZIndex, ElementMaterialFlags in_Flags)
        {
            var quad = new Quad();
            var aspect = new Vector2(in_AspectRatio, 1.0f);

            quad.TopLeft.Position = in_Position + (in_TopLeft * in_Scale * aspect).Rotate(in_Rotation) / aspect;
            quad.BottomLeft.Position = in_Position + (in_BottomLeft * in_Scale * aspect).Rotate(in_Rotation) / aspect;
            quad.TopRight.Position = in_Position + (in_TopRight * in_Scale * aspect).Rotate(in_Rotation) / aspect;
            quad.BottomRight.Position = in_Position + (in_BottomRight * in_Scale * aspect).Rotate(in_Rotation) / aspect;

            if (in_Sprite != null && in_NextSprite != null)
            {
                var begin = new Vector2(
                    in_Sprite.Start.X / in_Sprite.Texture.Width,
                    in_Sprite.Start.Y / in_Sprite.Texture.Height);

                var nextBegin = new Vector2(
                    in_NextSprite.Start.X / in_NextSprite.Texture.Width,
                    in_NextSprite.Start.Y / in_NextSprite.Texture.Height);

                var end = begin + new Vector2(
                    in_Sprite.Dimensions.X / in_Sprite.Texture.Width,
                    in_Sprite.Dimensions.Y / in_Sprite.Texture.Height);

                var nextEnd = nextBegin + new Vector2(
                    in_NextSprite.Dimensions.X / in_NextSprite.Texture.Width,
                    in_NextSprite.Dimensions.Y / in_NextSprite.Texture.Height);

                begin = (1.0f - in_SpriteFactor) * begin + in_SpriteFactor * nextBegin;
                end = (1.0f - in_SpriteFactor) * end + in_SpriteFactor * nextEnd;

                if ((in_Flags & ElementMaterialFlags.MirrorX) != 0) (begin.X, end.X) = (end.X, begin.X); // Mirror X
                if ((in_Flags & ElementMaterialFlags.MirrorY) != 0) (begin.Y, end.Y) = (end.Y, begin.Y); // Mirror Y

                quad.TopLeft.Uv = begin;
                quad.TopRight.Uv = new Vector2(end.X, begin.Y);
                quad.BottomLeft.Uv = new Vector2(begin.X, end.Y);
                quad.BottomRight.Uv = end;
                quad.Texture = in_Sprite.Texture;
            }

            quad.TopLeft.Color = in_Color * in_GradientTopLeft;
            quad.TopRight.Color = in_Color * in_GradientTopRight;
            quad.BottomLeft.Color = in_Color * in_GradientBottomLeft;
            quad.BottomRight.Color = in_Color * in_GradientBottomRight;

            quad.ZIndex = in_ZIndex;
            quad.Additive = (in_Flags & ElementMaterialFlags.AdditiveBlending) != 0;
            quad.LinearFiltering = (in_Flags & ElementMaterialFlags.LinearFiltering) != 0;

            _quads.Add(quad);
        }

        public void SetShader(ShaderProgram in_Param)
        {
            _shader = in_Param;
            _shader.Use();
        }

        /// <summary>
        /// Clears the quad buffer and starts a new rendering batch.
        /// </summary>
        public void Start()
        {
            _quads.Clear();

            GL.ActiveTexture(TextureUnit.Texture0);
            BeginBatch();
        }

        /// <summary>
        /// Draws the quads in the quad buffer.
        /// </summary>
        public void End()
        {
            var ordered = _quads.OrderByDescending(x => x.ZIndex);
            foreach (var quad in ordered)
            {
                int id = quad.Texture?.GlTex?.Id ?? -1;

                if (id != TextureId || Additive != quad.Additive || LinearFiltering != quad.LinearFiltering || NumVertices >= MaxVertices)
                {
                    EndBatch();
                    BeginBatch();

                    quad.Texture?.GlTex?.Bind();

                    TextureId = id;
                    Additive = quad.Additive;
                    LinearFiltering = quad.LinearFiltering;
                }

                PushQuad(quad);
            }

            if (BatchStarted)
                EndBatch();
        }
    }
}
