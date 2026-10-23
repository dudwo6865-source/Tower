# 3번 콘솔 디자인 — 개별 PNG 두 번째 제작본

위치: `Assets/Artasseet/UI/Texter/MissionPopup/ConsoleV2`

사용자가 다시 첨부한 3번 시안을 기준으로 내장 이미지 생성 도구로 재제작했습니다. 은색 모서리, 청록 조명, 주황 선택 표시를 유지하고 두꺼운 검은 외곽선과 바깥 그림자를 제외하도록 제작했습니다. 남색 면과 금속 자체의 입체 음영은 남아 있습니다. 시안의 픽셀을 직접 잘라낸 결과가 아니므로 미세한 형태 차이는 있습니다.

## 파일 11개

- Panel: 빈 제목 영역을 포함한 팝업 바탕
- RowNormal / RowSelected: 일반·선택 스테이지 항목
- ButtonPrimary / ButtonSecondary: 출전·닫기용 빈 버튼
- Close: X 닫기 버튼
- BadgeNormal / BadgeSelected: 일반·선택 번호 배지
- StarEarned / StarEmpty: 획득·미획득 별
- SelectionArrow: 선택 항목 왼쪽에 놓는 주황 삼각형

글자와 숫자는 TextMeshPro로 추가합니다. PNG는 실제 알파 채널을 포함하며 원본 여백은 그대로 보존했습니다. 유니티 Project 창에서 PNG를 펼쳐 내부 `Mission_ConsoleV2_...` 스프라이트를 Image에 넣으면 여백을 제외한 지정 영역이 사용됩니다. 아이콘과 패널은 Simple, 원래 비율 유지가 기본입니다. 항목과 버튼은 Sliced로 가로 크기를 조절할 수 있습니다. 테두리가 두꺼워 보이면 Image의 Pixels Per Unit Multiplier를 올립니다.

씬에는 배치하지 않았습니다. PNG의 투명도와 스프라이트 영역은 정적으로 확인했습니다. 생성 프롬프트는 ConsoleV2.prompts.json, 파일 해상도와 지정 영역은 ConsoleV2.validation.json에 기록했습니다.
