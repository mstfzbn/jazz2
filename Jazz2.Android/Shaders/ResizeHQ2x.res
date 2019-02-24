{
    "Vertex": "
#version 300 es 

uniform mat4 ModelView;
uniform mat4 Projection;

uniform vec2 mainTexSize;

in vec4 Color;
in vec3 Position;
in vec2 TexCoord;

out vec2 vTexcoord0;
out vec4 vTexcoord1;
out vec4 vTexcoord2;
out vec4 vTexcoord3;
out vec4 vTexcoord4;

void main() {
    gl_Position = Projection * (ModelView * vec4(Position, 1.0));
    
    float x = 0.5 / mainTexSize.x;
    float y = 0.5 / mainTexSize.y;
    vec2 dg1 = vec2( x, y);
    vec2 dg2 = vec2(-x, y);
    vec2 dx = vec2(x, 0.0);
    vec2 dy = vec2(0.0, y);

    vTexcoord0 = TexCoord;
    vTexcoord1.xy = vTexcoord0.xy - dg1;
    vTexcoord1.zw = vTexcoord0.xy - dy;
    vTexcoord2.xy = vTexcoord0.xy - dg2;
    vTexcoord2.zw = vTexcoord0.xy + dx;
    vTexcoord3.xy = vTexcoord0.xy + dg1;
    vTexcoord3.zw = vTexcoord0.xy + dy;
    vTexcoord4.xy = vTexcoord0.xy + dg2;
    vTexcoord4.zw = vTexcoord0.xy - dx;
}",

"Fragment": "
#version 300 es 
precision highp float;

uniform sampler2D mainTex;

const float mx = 0.325;      // start smoothing wt.
const float k = -0.250;      // wt. decrease factor
const float max_w = 0.25;    // max filter weigth
const float min_w =-0.05;    // min filter weigth
const float lum_add = 0.25;  // effects smoothing

in vec2 vTexcoord0;
in vec4 vTexcoord1;
in vec4 vTexcoord2;
in vec4 vTexcoord3;
in vec4 vTexcoord4;

out vec4 vFragColor;

void main() {
    vec3 c00 = texture(mainTex, vTexcoord1.xy).xyz; 
    vec3 c10 = texture(mainTex, vTexcoord1.zw).xyz; 
    vec3 c20 = texture(mainTex, vTexcoord2.xy).xyz; 
    vec3 c01 = texture(mainTex, vTexcoord4.zw).xyz; 
    vec3 c11 = texture(mainTex, vTexcoord0.xy).xyz; 
    vec3 c21 = texture(mainTex, vTexcoord2.zw).xyz; 
    vec3 c02 = texture(mainTex, vTexcoord4.xy).xyz; 
    vec3 c12 = texture(mainTex, vTexcoord3.zw).xyz; 
    vec3 c22 = texture(mainTex, vTexcoord3.xy).xyz; 
    vec3 dt = vec3(1.0, 1.0, 1.0);

    float md1 = dot(abs(c00 - c22), dt);
    float md2 = dot(abs(c02 - c20), dt);

    float w1 = dot(abs(c22 - c11), dt) * md2;
    float w2 = dot(abs(c02 - c11), dt) * md1;
    float w3 = dot(abs(c00 - c11), dt) * md2;
    float w4 = dot(abs(c20 - c11), dt) * md1;

    float t1 = w1 + w3;
    float t2 = w2 + w4;
    float ww = max(t1, t2) + 0.0001;

    c11 = (w1 * c00 + w2 * c20 + w3 * c22 + w4 * c02 + ww * c11) / (t1 + t2 + ww);

    float lc1 = k / (0.12 * dot(c10 + c12 + c11, dt) + lum_add);
    float lc2 = k / (0.12 * dot(c01 + c21 + c11, dt) + lum_add);

    w1 = clamp(lc1 * dot(abs(c11 - c10), dt) + mx, min_w, max_w);
    w2 = clamp(lc2 * dot(abs(c11 - c21), dt) + mx, min_w, max_w);
    w3 = clamp(lc1 * dot(abs(c11 - c12), dt) + mx, min_w, max_w);
    w4 = clamp(lc2 * dot(abs(c11 - c01), dt) + mx, min_w, max_w);

    vFragColor.xyz = w1 * c10 + w2 * c21 + w3 * c12 + w4 * c01 + (1.0 - w1 - w2 - w3 - w4) * c11;
}"

}