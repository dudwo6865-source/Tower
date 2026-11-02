# 분리형 체력바 스프라이트 생성 기록

내장 image_gen으로 A안 컨셉을 참조하여 6개 레이어를 각각 생성했습니다. PNG 원본과 투명도를 그대로 보존합니다. Unity 가져오기 설정은 여백을 제외한 단일 하위 스프라이트와 RGB 모두 R 채널을 사용하는 무채색 매핑, 무압축, 밉맵 끄기를 지정합니다.

## unit-frame.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\unit-a.png

Create a production Unity UI PNG sprite using the attached minimal clipped-corner metal health bar concept as visual reference. Output ONLY the frame layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Isolate the metallic outer border frame. The complete inner health reservoir must be a transparent empty hole. Preserve the reference border silhouette and bevels. Remove the fill, dark inner backing, all labels and top emblem. Neutral silver and gray industrial metal, highlights sufficiently light to tint using Image Color.

## unit-background.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\unit-a.png

Create a production Unity UI PNG sprite using the attached minimal clipped-corner metal health bar concept as visual reference. Output ONLY the background layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Output only a solid dark neutral gray inner reservoir/backplate, clipped corners matching the reference bar's inside shape. No metallic outline or rim whatsoever, no fill, no glow. Nearly uniform charcoal with very subtle grayscale depth. This is the empty health area backing layer.

## unit-fill.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\unit-a.png

Create a production Unity UI PNG sprite using the attached minimal clipped-corner metal health bar concept as visual reference. Output ONLY the fill layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Output only a FULL 100 percent filled inner health strip, spanning the entire width, with no empty portion. No border, no backing. Bright white with restrained light gray shading, zero saturation, suitable for multiplication by Unity Image Color. Crisp clipped corners matching the reference reservoir, no glow outside silhouette. Nearly uniform along its entire horizontal length so horizontal Filled cropping remains clear.

## boss-frame.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\boss-a.png

Create a production Unity UI PNG sprite using the attached angular armored boss health bar concept as visual reference. Output ONLY the frame layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Isolate the metallic outer border frame. The complete inner health reservoir must be a transparent empty hole. Preserve the reference border silhouette and bevels. Remove the fill, dark inner backing, all labels and top emblem. Neutral silver and gray industrial metal, highlights sufficiently light to tint using Image Color.

## boss-background.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\boss-a.png

Create a production Unity UI PNG sprite using the attached angular armored boss health bar concept as visual reference. Output ONLY the background layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Output only a solid dark neutral gray inner reservoir/backplate, clipped corners matching the reference bar's inside shape. No metallic outline or rim whatsoever, no fill, no glow. Nearly uniform charcoal with very subtle grayscale depth. This is the empty health area backing layer.

## boss-fill.png

참조: D:\Game\Tank!\Docs\HealthBarConcepts\boss-a.png

Create a production Unity UI PNG sprite using the attached angular armored boss health bar concept as visual reference. Output ONLY the fill layer, no other layers. Strict neutral grayscale: R=G=B everywhere, no colored tint. Real transparent alpha background, not a checkerboard painting. Straight-on horizontal sprite, tightly framed with minimal transparent padding, no perspective, no surrounding scene, no text, no numbers, no symbols, no title, no shadow outside silhouette. Wide low rectangular composition. Output only a FULL 100 percent filled inner health strip, spanning the entire width, with no empty portion. No border, no backing. Bright white with restrained light gray shading, zero saturation, suitable for multiplication by Unity Image Color. Crisp clipped corners matching the reference reservoir, no glow outside silhouette. Nearly uniform along its entire horizontal length so horizontal Filled cropping remains clear.
