# 경량형 전투 UI 패널
경량형 시안을 바탕으로 새로 제작한 빈 패널 3종입니다. 글자, 아이콘, 수치, 게이지는 Unity의 자식 UI로 올립니다.

## 적용
1. Project에서 이 폴더의 PNG 왼쪽 펼침 화살표를 눌러 내부의 동명 스프라이트를 선택합니다.
2. Canvas 아래 UI > Image를 만들고 Source Image에 해당 스프라이트를 넣습니다.
3. Image Type을 Sliced로 지정하고 Fill Center를 켭니다. Preserve Aspect는 끕니다.
4. Rect Transform으로 크기를 조절합니다. 장식 배경의 Raycast Target은 끄고 실제 버튼의 Image에는 켭니다.
5. 버튼에는 Button 컴포넌트를 붙이고 아이콘(Image)과 글자(TextMeshPro)를 자식으로 배치합니다.

이미지는 Sprite (2D and UI) / Multiple로 설정했습니다. 각 PNG에는 여백을 제외한 스프라이트 1개만 들어 있습니다. 원본 PNG의 투명 여백은 유지되며 Unity에서는 지정한 사각형만 사용합니다. PNG와 .meta를 함께 옮겨야 설정이 유지됩니다.

## 파일과 권장 크기
- TopMenuPanel.png: 자원, 시간, 상단 메뉴 배경. 시작 크기 420 × 88 UI 단위. 짧은 자원창은 200 × 72로 조절합니다.
- BottomInfoPanel.png: 하단 선택 정보, 명령창의 공통 배경. 시작 크기 530 × 140 UI 단위.
- CommandButton.png: 명령/건설 버튼 기본 상태 배경. 시작 크기 96 × 96 UI 단위.

## 저장된 Border 값
Sprite Editor 기준 Left / Bottom / Right / Top, 원본 픽셀 단위입니다.
- TopMenuPanel: 170 / 140 / 170 / 140
- BottomInfoPanel: 180 / 170 / 180 / 170
- CommandButton: 230 / 210 / 230 / 210

Pixels Per Unit은 1000입니다. Canvas의 Reference Pixels Per Unit이 기본값 100이고 Image의 Pixels Per Unit Multiplier가 1일 때 테두리는 원본의 1/10 UI 단위로 표시됩니다. 프레임을 더 얇게 하려면 Image의 Pixels Per Unit Multiplier를 올립니다. 작게 줄이면서 모서리가 눌리면 패널 크기를 늘리거나 이 배율을 올립니다.

## 버튼 상태
이 패키지는 기본 상태 이미지 1종을 제공합니다. Button > Transition > Color Tint에서 Highlighted / Pressed / Disabled 색을 조절할 수 있습니다. 선택 유지 표시와 아이콘은 별도의 자식 UI로 구성합니다.

## 제작 및 확인
내장 이미지 생성 도구로 제작했습니다. 실제 알파 채널, 스프라이트 사각형과 Border 범위를 확인했습니다. Unity 화면에서의 최종 크기와 눌림 상태는 씬에서 확인해 주세요. 기존 씬과 UI 연결은 수정하지 않았습니다. 생성 프롬프트는 generation-prompts.txt에 기록했습니다.

