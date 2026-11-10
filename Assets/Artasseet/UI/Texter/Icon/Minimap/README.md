# 미니맵 아이콘

투명 PNG 9종입니다. 흰색 부분에 소속 색을 곱해서 사용합니다.
보스·알·본부·스포너 4종은 외곽선 없는 순백색 도형으로 다시 제작했습니다. 256×256 PNG와 같은 모양의 SVG 원본을 제공합니다.
이 4종의 최종 결과는 이미지 생성 시안의 가장자리 잡음을 피하기 위해 벡터 도형으로 직접 제작했습니다. 나머지 5종은 기존 이미지 생성 결과를 유지합니다.
동봉한 메타 파일은 단일 스프라이트·최대 256·밉맵 없음·압축 없음으로 설정합니다.
기존 PNG의 경로와 메타 파일을 유지하므로 연결된 스프라이트 참조는 유지됩니다. 씬과 프리팹의 연결은 변경하지 않습니다.
Unity에는 PNG를 연결합니다. SVG는 수정용 원본이며 별도 SVG 가져오기 플러그인이 필요하지 않습니다.

## 파일별 용도

- Minimap_Headquarters.png: 본부, 안테나가 있는 지휘 건물입니다.
- Minimap_Spawner.png: 적 생성 스포너, 중앙을 감싼 외계 둥지입니다.
- Minimap_Boss.png: 보스, 두 뿔이 있는 머리입니다.
- Minimap_Egg.png: 알 파괴 미션의 알, 금이 간 알입니다.
- Minimap_Unit.png: 아군·적 일반 유닛에 공용으로 쓰는 헬멧입니다.
- Minimap_Building.png: 아군·적 일반 건물에 공용으로 쓰는 요새입니다.
- Minimap_DamageAlert.png: 본부 피격 경고용 링입니다. 중앙이 투명합니다.
- Minimap_Selected.png: 선택 상태를 겹쳐 표시할 때 쓸 네 모서리 테두리입니다.
- Minimap_HillRoute.png: 언덕 진입 방향용 화살표입니다. 위쪽을 오르막 방향으로 회전해 사용합니다.

## 현재 표시 조건과 사용 방법

본부·스포너는 MinimapBlipManager의 '본부 아이콘'과 '스포너 아이콘'에 연결할 수 있습니다.
대상별 지정은 SelectableEntity가 있는 프리팹 루트에 MinimapIcon을 붙이고 '아이콘 이미지'에 연결합니다.
'색 방식'을 '소속 색 (아군/적)'으로 두면 아군 유닛·아군 건물·적 유닛·적 건물 색과 발견 후 시야 밖의 투명도를 그대로 사용합니다.
보스와 알도 이 방식으로 각각 연결합니다. 미션 목표의 항상 표시 여부는 기존 미니맵 관리자의 미션 조건을 따릅니다.
'아이콘 크기(픽셀)'은 주요 대상 20~24부터 조정하는 것을 권장합니다. 일반 유닛 기본 크기 6픽셀에서는 세부 모양을 알아보기 어렵습니다.

피격 링·선택 테두리·언덕 화살표는 교체용 이미지입니다. 현재 피격 링과 언덕 화살표는 코드로 그리며 선택은 크기 확대로 표현하므로, 이 세 이미지는 기존 인스펙터에 넣는 것만으로 자동 적용되지 않습니다.
절벽 윤곽·안개·카메라 화면 범위는 면적과 선을 나타내는 표시여서 대상 아이콘으로 대체하지 않습니다.

## 최초 이미지 시안 제작 프롬프트

도구: 내장 image_gen (CLI 사용 안 함).
공통 지시: Unity RTS minimap sprite, one isolated icon, genuine transparent alpha background, square canvas, bright white/light gray with crisp near-black outline, flat bold sci-fi military pictogram, large simple shapes readable at 16–24 pixels, centered with padding, no text/glow/shadow/watermark.
각 대상 지시:

- Headquarters: fortified futuristic command headquarters bunker with broad base, central command tower and antenna.
- Spawner: alien spawning nest, three thick curved mandibles surrounding a central dark opening.
- Boss: powerful alien boss head, broad skull with two large horns and two angular dark eyes.
- Egg: one upright alien egg with a single broad zigzag crack, simple oval silhouette.
- Unit: futuristic infantry helmet front view, one broad dark visor, extremely simple silhouette.
- Building: generic square fortified building, flat roof, two chunky corner pillars and dark doorway, no tower.
- DamageAlert: thick circular warning ring with four short outward cardinal spikes, transparent empty center.
- Selected: four thick right-angle corner brackets forming a square selection frame, transparent empty center.
- HillRoute: upward pointing double chevron, two chunky separated chevrons.
