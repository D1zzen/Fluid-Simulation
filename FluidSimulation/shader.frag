#version 460 core
out vec4 FragColor;

in vec2 texCoord;

uniform sampler2D tex;

void main()
{
    //vec2 pixels = res / pixelSize;
    //vec2 pixelated = floor(pixels * texCoord) / pixels;
    vec2 flipY = vec2(texCoord.x, 1 - texCoord.y);
    FragColor = texture(tex, flipY);
}