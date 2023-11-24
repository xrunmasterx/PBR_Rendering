#version 330 core
out vec4 FragColor;

struct Light {
    vec3 position;

    vec3 ambient;
    vec3 diffuse;
    vec3 specular;

    vec3 color;
};

in vec3 FragPos;  
in vec3 Normal;  
in vec2 TexCoords;

uniform float roughness;
uniform float ao;
uniform sampler2D texture_diffuse1;
uniform sampler2D texture_specular1;
uniform samplerCube irradianceMap;
uniform samplerCube prefilterMap;
uniform sampler2D brdfLUT;
uniform vec3 viewPos;
uniform Light light[4];
uniform samplerCube skybox;
uniform float light_intensity;
uniform float f_albedo;
uniform float f_roughness;
uniform float f_metallic;

const float PI=3.14159265359;

//D
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness*roughness;
    float a2 = a*a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

//F
vec3 fresnelSchlick(float cosTheta,vec3 F0)
{
    return F0+(1.0-F0)*pow(clamp(1.0-cosTheta,0.0,1.0),5.0);
}
vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
} 
//G
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

void main()
{

    float t_roughness=roughness*f_roughness;
    vec3 N=Normal;
    vec3 V=normalize(viewPos-FragPos);
    vec3 albedo = pow(texture(texture_diffuse1, TexCoords).rgb, vec3(2.2))*f_albedo;
    float metallic=texture(texture_specular1, TexCoords).r*f_metallic;
    vec3 R = reflect(-V, N); 
    vec3 F0=vec3(0.04);
    F0=mix(F0,albedo,metallic);
    vec3 Lo=vec3(0.0);

    for(int i=0;i<4;i++)
    {
        //light radiance
        vec3 L=normalize(light[i].position-FragPos);
        vec3 H=normalize(V+L);
        float distance=length(light[i].position-FragPos);
        float attenuation=1.0/(distance*distance);
        vec3 radiance=light_intensity*(light[i].ambient+light[i].diffuse+light[i].specular)*light[i].color*attenuation;

        //Cook-Torrance BRDF
        float NDF=DistributionGGX(N,H,t_roughness);
        float G=GeometrySmith(N,V,L,t_roughness);
        vec3 F=fresnelSchlick(max(dot(H,V),0.0),F0);

        //specular
        vec3 numerator=NDF*G*F;
        float denominator=4.0*max(dot(N,V),0.0)*max(dot(N,L),0.0)+0.0001;
        vec3 specular=numerator/denominator;

        //diffuse
        vec3 kS=F;
        vec3 kD=vec3(1.0)-kS;
        kD*=1.0-metallic;
        float NdotL=max(dot(N,L),0.0);
        //vec3 diffuse=kD*albedo/PI;

        vec3 diffuse=kD*albedo/PI;
        Lo+=(diffuse+specular)*radiance*NdotL;
    }

    vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, t_roughness);
    
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallic;
    
    vec3 irradiance = texture(irradianceMap, N).rgb;
    vec3 diffuse      = irradiance * albedo;
    
    // sample both the pre-filter map and the BRDF lut and combine them together as per the Split-Sum approximation to get the IBL specular part.
    const float MAX_REFLECTION_LOD = 4.0;
    vec3 prefilteredColor = textureLod(prefilterMap, R,  t_roughness * MAX_REFLECTION_LOD).rgb;    
    vec2 brdf  = texture(brdfLUT, vec2(max(dot(N, V), 0.0), t_roughness)).rg;
    vec3 specular = prefilteredColor * (F * brdf.x + brdf.y);

    vec3 ambient = (kD * diffuse + specular) * ao;
    
    vec3 color = ambient + Lo;

    // HDR tonemapping
    color = color / (color + vec3(1.0));
    // gamma correct
    color = pow(color, vec3(1.0/2.2)); 

    FragColor = vec4(color , 1.0);
}