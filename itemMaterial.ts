import {
  CustomMaterial,
  type Scene,
  Texture,
  type Material,
} from '../libs/babylon/exports';

/**
 * Is used as mask for item options
 * low 4 bits - item lvl
 * 5th bit - is excellent
 */
const ITEM_OPTIONS_UNIFORM_NAME = `itemOptions`;

export function createItemMaterial(
  scene: Scene,
  basedOn?: Material,
  diffuseTexture?: Texture
) {
  const simpleMaterial = new CustomMaterial('itemMaterial', scene);

  simpleMaterial.diffuseColor.setAll(1);
  if (basedOn) {
    simpleMaterial.alpha = basedOn.alpha;
    simpleMaterial.transparencyMode = basedOn.transparencyMode;
    simpleMaterial.backFaceCulling = basedOn.backFaceCulling;
  }

  simpleMaterial.specularColor.setAll(0);

  if (diffuseTexture) {
    diffuseTexture.isBlocking = false;
    diffuseTexture.updateSamplingMode(Texture.NEAREST_NEAREST);

    simpleMaterial.diffuseTexture = diffuseTexture;
    simpleMaterial.useAlphaFromDiffuseTexture = true;
  } else {
    return simpleMaterial;
  }

  simpleMaterial.AddUniform(ITEM_OPTIONS_UNIFORM_NAME, 'int', 0);
  simpleMaterial.AddUniform('time', 'float', 0);
  simpleMaterial.AddUniform('glowColor', 'vec3', null);

  simpleMaterial.Fragment_Definitions(`
    float noise2(vec2 coords){
      vec2 texSize = vec2(1.0);
      vec2 pc = coords * texSize;
      vec2 base = floor(pc);
      float s1 = getRand((base + vec2(0.0,0.0)) / texSize);
      float s2 = getRand((base + vec2(1.0,0.0)) / texSize);
      float s3 = getRand((base + vec2(0.0,1.0)) / texSize);
      float s4 = getRand((base + vec2(1.0,1.0)) / texSize);
      vec2 f = smoothstep(0.0, 1.0, fract(pc));
      float px1 = mix(s1,s2,f.x);
      float px2 = mix(s3,s4,f.x);
      float result = mix(px1,px2,f.y);
      return result;
    }
  `);

  simpleMaterial.Fragment_Before_FragColor(`
    int iItemOptions = ${ITEM_OPTIONS_UNIFORM_NAME};
    int iItemLvl = iItemOptions & 0x0F;
    bool bIsExcellent = (iItemOptions & 0x10) != 0;
    
    float wave = float(int(time * 1000.0) % 10000) * 0.0001;
    vec3 view = normalize(vEyePosition.xyz-vPositionW) + vNormalW + vec3(10000.5);
    float lvl = float(iItemLvl);
    
    // Determine glow parameters based on item level
    vec3 effectColor = glowColor.x > 0.0 ? glowColor : vec3(1.0, 0.8, 0.0); // Use custom color or default gold
    float brightness = 1.0;
    float ghostIntensity = 0.0;
    
    if (lvl < 1.5) { // 0,1 - no effects
      brightness = 1.0;
      ghostIntensity = 0.0;
    }
    else if (lvl < 4.5) { // 2,3,4 - basic color tint
      effectColor = glowColor.x > 0.0 ? glowColor : vec3(0.8, 0.4, 0.4); // Red tint
      brightness = 1.2;
      ghostIntensity = 0.0;
      float mixAmount = (1.0 + sin(time * 4.0)) / 2.0;
      vec3 minColor = vec3(0.4,0.3,0.3);
      vec3 maxColor = vec3(0.7,0.5,0.5);
      vec3 vPartColor = mix(minColor, maxColor, mixAmount);
      color.rgb = color.rgb * vPartColor;
    }
    else if (lvl < 6.5) { // 5,6 - blue tint
      effectColor = glowColor.x > 0.0 ? glowColor : vec3(0.4, 0.6, 0.8); // Blue tint
      brightness = 1.3;
      ghostIntensity = 0.0;
      float mixAmount = (1.0 + sin(time * 4.0)) / 2.0;
      vec3 minColor = vec3(0.3,0.4,0.4);
      vec3 maxColor = vec3(0.5,0.6,0.6);
      vec3 vPartColor = mix(minColor, maxColor, mixAmount);
      color.rgb = color.rgb * vPartColor;
    }
    else if (lvl < 8.5) { // 7,8 - low ghosting
      effectColor = glowColor.x > 0.0 ? glowColor : vec3(1.0, 0.8, 0.0); // Gold
      brightness = 1.5;
      ghostIntensity = 0.3;
    }
    else if (lvl < 9.5) { // 9 - medium ghosting
      effectColor = glowColor.x > 0.0 ? glowColor : vec3(1.0, 0.8, 0.0); // Gold
      brightness = 2.0;
      ghostIntensity = 0.6;
    }
    else { // 10+ - full ghosting
      effectColor = glowColor.x > 0.0 ? glowColor : vec3(1.0, 0.8, 0.0); // Gold
      brightness = 2.5;
      ghostIntensity = 1.0;
    }
    
    // Apply ghosting effects if level 7+
    if (lvl >= 6.5) {
      float subtlePulse = (1.0 + sin(time * 1.5)) * 0.03 + 0.97;
      float shimmer = (1.0 + sin(time * 20.0 + vNormalW.x * 12.0)) * 0.15 + 0.85;
      
      // Dynamic ghosting effect - scale with ghostIntensity
      vec2 ghostOffset1 = vec2(sin(time * 4.0) * 0.035, cos(time * 3.5) * 0.035) * ghostIntensity;
      vec2 ghostOffset2 = vec2(sin(time * 5.5 + 2.1) * 0.025, cos(time * 4.8 + 1.8) * 0.025) * ghostIntensity;
      vec2 ghostOffset3 = vec2(sin(time * 6.2 + 4.2) * 0.02, cos(time * 5.9 + 3.7) * 0.02) * ghostIntensity;
      vec2 ghostOffset4 = vec2(sin(time * 3.3 + 1.1) * 0.015, cos(time * 6.7 + 2.3) * 0.015) * ghostIntensity;
      
      vec4 ghost1 = texture2D(diffuseSampler, vDiffuseUV + ghostOffset1);
      vec4 ghost2 = texture2D(diffuseSampler, vDiffuseUV + ghostOffset2);
      vec4 ghost3 = texture2D(diffuseSampler, vDiffuseUV + ghostOffset3);
      vec4 ghost4 = texture2D(diffuseSampler, vDiffuseUV + ghostOffset4);
      
      // Apply metallic effect
      vec3 metallic = effectColor * 2.0;
      
      // Combine original with enhanced ghosting effects
      color.rgb = color.rgb * metallic * (brightness * 0.8) * subtlePulse;
      color.rgb += ghost1.rgb * (0.5 * ghostIntensity) * shimmer;
      color.rgb += ghost2.rgb * (0.4 * ghostIntensity) * shimmer;
      color.rgb += ghost3.rgb * (0.3 * ghostIntensity) * shimmer;
      color.rgb += ghost4.rgb * (0.2 * ghostIntensity) * shimmer;
      
      // Additional brightness boost for higher levels
      if (lvl >= 9.5) {
        float extraGlow = (lvl - 9.0) * 0.5;
        float glowEffect = (1.0 + sin(time * 4.0)) * 0.1 + 0.8;
        color.rgb += effectColor * glowEffect * extraGlow;
      }
    }
`);

  let time = 0;

  scene.onReadyObservable.addOnce(() => {
    scene.onBeforeRenderObservable.add(() => {
      time += scene.getEngine()!.getDeltaTime()! / 1000;
    });

    simpleMaterial.onBindObservable.add(mesh => {
      const effect = simpleMaterial.getEffect();
      if (!effect) return;
      effect.setFloat('time', time);

      let itemOptions = 0;

      if (mesh.metadata?.itemLvl) {
        itemOptions = mesh.metadata.itemLvl;
      }

      if (mesh.metadata?.isExcellent) {
        itemOptions |= 0x10;
      }

      effect.setInt(ITEM_OPTIONS_UNIFORM_NAME, itemOptions);

      // Set custom glow color if provided
      if (mesh.metadata?.glowColor) {
        effect.setVector3('glowColor', mesh.metadata.glowColor);
      } else {
        effect.setVector3('glowColor', { x: 0, y: 0, z: 0 }); // Use default colors
      }

      if (diffuseTexture) {
        effect.setTexture('diffuseSampler', diffuseTexture);
      }
    });
  });

  return simpleMaterial;
}
