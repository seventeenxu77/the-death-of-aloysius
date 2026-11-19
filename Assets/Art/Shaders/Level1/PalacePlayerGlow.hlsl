#ifndef ALOYSIUS_PALACE_PLAYER_GLOW
#define ALOYSIUS_PALACE_PLAYER_GLOW
float PalacePlayerGlowFalloff(float2 world,float4 area)
{
    float distanceToPlayer=distance(world,area.xy)/max(area.z,.05);
    return 1-smoothstep(area.w,1,distanceToPlayer);
}
#endif
