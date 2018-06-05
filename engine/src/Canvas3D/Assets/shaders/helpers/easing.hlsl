#ifndef __EASING_HLSL__
#define __EASING_HLSL__

float easeOutBounce1(float t) {
    float a = 7.5625;
    float t2 = t - (1.5 / 2.75);
    float t3 = t - (2.25 / 2.75);
    float t4 = t - (2.625 / 2.75);

    return t < 1 / 2.75 ? a * t * t :
           t < 2 / 2.75 ? a * t2 * t2 + 0.75 :
           t < 2.5 / 2.75 ? a * t3 * t3 + 0.9375 :
           a * t4 * t4 + 0.984375;
}

float easeInOutBounce1(float time) {
    // goal: [0, 0.5] is 0.5 + (-1) * easeOutBounce((-1) * (t * 2 - 1)) * 0.5
    //       [0.5, 1] is 0.5 + ( 1) * easeOutBounce(( 1) * (t * 2 - 1)) * 0.5
    float sig = sign(time - 0.5f);     // either -1 (t < 0.5) or 1 (t > 0.5)
    return 0.5 + sig * easeOutBounce1(sig * (time * 2 - 1)) * 0.5;
}

float easeOutBack1(float time) {
    return time * time * (2.70158f * time - 1.70158f);
}

float easeInOutBack1(float time) {
    float sig = sign(time - 0.5f);
    return 0.5 + sig * easeOutBack1(sig * (time * 2 - 1)) * 0.5f;
}

// vec2
float2 easeOutBounce2(float2 t) {
    return float2(easeOutBounce1(t.x), easeOutBounce1(t.y));
}

float2 easeInOutBounce2(float2 t) {
    return float2(easeInOutBounce1(t.x), easeInOutBounce1(t.y));
}

float2 easeOutBack2(float2 t) {
    return float2(easeOutBack1(t.x), easeOutBack1(t.y));
}

float2 easeInOutBack2(float2 t) {
    return float2(easeInOutBack1(t.x), easeInOutBack1(t.y));
}

#endif __EASING_HLSL__