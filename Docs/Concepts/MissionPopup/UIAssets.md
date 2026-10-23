# 미션 팝업 PNG 사용 안내

저장 위치: `Assets/Artasseet/UI/Texter/MissionPopup/`

내장 이미지 생성 도구로 2번·3번 시안을 참고하여 재제작한 투명 PNG 14개입니다. 원본 시안의 픽셀을 그대로 잘라낸 파일은 아니며, 분리 과정에서 세부 장식에 차이가 있습니다. 글자와 숫자는 TextMeshPro로 넣습니다.

## 구성

- `Holographic`: 2번 디자인입니다. Panel, RowNormal, RowSelected, ButtonPrimary, ButtonSecondary가 있습니다.
- `Console`: 3번 디자인입니다. 동일한 5종이 있으며, 선택 항목과 기본 버튼은 주황색입니다. Panel에는 빈 제목 영역이 포함되어 있습니다.
- `Shared`: 두 디자인에 공통으로 쓰는 Close, NumberBadge, StarEarned, StarEmpty입니다.

## 유니티에서 사용하기

1. Project 창에서 PNG 왼쪽 화살표를 펼치고 안의 `Mission_...` 스프라이트를 Image의 Source Image에 넣습니다. 각 PNG는 Sprite (2D and UI), Multiple 모드로 설정되어 있으며, PNG 하나마다 사용 영역을 지정한 스프라이트 하나가 들어 있습니다.
2. Panel은 Image Type을 Simple로 설정하고 원래 비율을 유지하는 것을 권장합니다. 제목, 목록, 하단 버튼은 자식 오브젝트로 배치합니다.
3. 목록 항목과 버튼에는 9-slice 경계가 설정되어 있습니다. 가로 길이를 바꿀 때 Image Type을 Sliced로 설정합니다. 원본 해상도가 크므로 테두리가 두꺼워 보이면 Image의 Pixels Per Unit Multiplier를 올려 조절합니다. 원래 비율 그대로 사용할 때는 Simple도 가능합니다.
4. RowNormal과 RowSelected는 같은 RectTransform 크기로 사용합니다. 항목 왼쪽에 NumberBadge와 숫자 텍스트, 오른쪽에 별 3개를 배치합니다. 획득 여부에 따라 StarEarned / StarEmpty를 교체합니다.
5. 아이콘은 Simple과 Preserve Aspect를 사용합니다. ButtonPrimary에는 출전, ButtonSecondary에는 닫기 등 원하는 텍스트를 자식으로 넣습니다.

PNG의 원본 알파 채널을 보존했습니다. 생성 이미지에 포함된 넓은 빈 공간과 매우 희미한 외곽 흔적은 스프라이트 영역에서 제외했습니다. PNG 자체의 캔버스에는 여백이 있으므로 유니티에서는 RawImage 대신 지정된 스프라이트를 사용합니다. 미세한 테두리 번짐은 실제 표시 크기에서 확인하고 Sprite Editor에서 경계를 조절할 수 있습니다.

밉맵과 텍스처 압축은 끄고, Clamp 및 Bilinear를 적용했습니다. 생성 프롬프트는 `UIAssets.prompts.json`, PNG 해상도·스프라이트 영역·투명 픽셀 검증 결과는 `UIAssets.validation.json`에 기록했습니다. PNG 투명도와 메타데이터를 정적으로 확인했으며 씬에는 아직 배치하지 않았습니다.
