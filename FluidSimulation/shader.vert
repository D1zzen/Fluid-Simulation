#version 460 core

const vec2 vertex[3] =
{
    vec2( -1.0, -1.0 ),
    vec2(  3.0, -1.0 ),
    vec2( -1.0,  3.0 )
};

out vec2 texCoord;

void main()
{
    gl_Position = vec4(vertex[gl_VertexID], 0.0, 1.0);
    texCoord = gl_Position.xy * 0.5 + 0.5;
}