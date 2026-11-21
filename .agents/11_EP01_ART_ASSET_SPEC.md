# 나노바나나 2D 아트 컨셉 시안 및 EP 1 에셋 명세서 (11_EP01_ART_ASSET_SPEC)

> **문서 목적**: `K-POL OS` 기반 2D 추리 게임의 비주얼 컨셉 시안 5가지를 비교 제안하고, EP 1(MVP) 구현에 필요한 전체 2D 스프라이트·CCTV·현장 사진·초상화 에셋 목록(총 23종)과 나노바나나(Nano Banana) 일관성 유지용 프롬프트 가이드를 정의합니다.

---

## 1. 아트 컨셉 시안 5가지 비교 (컨셉 선정용)

| 시안 | 컨셉명 | 비주얼 특징 및 레퍼런스 느낌 | 장점 | 단점 / 주의점 | 줌인 캡처 적합도 |
| :---: | :--- | :--- | :--- | :--- | :---: |
| **시안 1 (추천)** | **세미 리얼리스틱 코리안 느와르** | 현대 한국 범죄 스릴러 영화 톤의 사실적 2D 일러스트/페인팅. 차가운 슬레이트 그레이(`#12161C`) + 형광등 블루 + 경고 레드 포인트. | 현대 경찰 `K-POL OS`·CCTV·현장 사진과 이질감 0%. 나노바나나의 일관성 편집 강점 극대화. | 과도하게 실사화되지 않도록 브러시/일러스트 톤 통일 필요. | **최상** |
| **시안 2** | **하이 콘트라스트 그래픽 노블 (잉크 느와르)** | 굵은 검은 그림자, 거친 잉크 텍스처, 제한된 포인트 컬러(<더 울프 어몽 어스>, <맥스 페인> 코믹스풍). | 인디 게임 특유의 강렬한 개성과 예술성. 배경 단순화 가능. | 강한 검은 그림자가 CCTV 줌인 단서(투명 테이프, 팔찌) 가독성을 방해할 수 있음. | 중 |
| **시안 3** | **다큐멘터리 포토리얼리즘** | 실제 경찰 감식 카메라 플래시, 35mm 필름 그레인, 실제 편의점 CCTV 같은 극사실 실사풍(<허 스토리> 계열). | 압도적인 현장감과 소름 돋는 리얼리티. | 인물 초상화 생성 시 실존 인물 유사성/불쾌한 골짜기 주의 필요. | **최상** |
| **시안 4** | **레트로 관공서 OS + 하이비트 픽셀 아트** | 구형 윈도우풍 경찰 인트라넷 UI + 정교한 2D 픽셀 아트(<페이퍼즈 플리즈>, <황금 우상 사건> 계열). | 인디 추리 팬층 호감도 최상. 전체 UI 통일감이 뛰어남. | `2.5x` 줌인 캡처(번호판, 팔찌) 시 픽셀 깨짐 문제로 고해상도 줌인 전용 스프라이트 이중 제작 필요. | 하~중 |
| **시안 5** | **미니멀 벡터·블루프린트 전술 UI** | 디테일을 절제한 플랫 벡터 일러스트 + 깔끔한 다크 전술 OS UI(<911 오퍼레이터>, <디스 이즈 더 폴리스> 계열). | 화풍 통일이 가장 쉽고 OS UI 가독성이 최상. | 현장 사진·CCTV에서 미세 물증(혈흔, 띠지, 테이프)을 찾는 현장감이 다소 건조함. | 중 |

* **최종 확정 아트 컨셉**: **인디 리소그래프(Risograph) & 거친 스크린프린트 일러스트 (고밀도 한국 현실 공간 + 현실 반영 저채도 멀티컬러)**
  * **확정 특징**: 굵은 손그림 잉크 외곽선, 거친 종이 섬유 질감, 살짝 어긋난 잉크 겹침, 무광 건조 잉크 마감으로 **AI 특유의 플라스틱 광택을 완전 제거**.
  * **색감 디렉션**: 차가운 단색에만 머무르지 않고, **실제 공간의 고유 색감(따뜻한 형광등 아이보리, 진열대 상품의 차분한 색, 빨간 소화기, 나무/베이지 톤, 노란 은행 띠지 등)을 차분한 저채도(Muted Natural Risograph Palette)로 자연스럽게 반영**.
  * **CCTV 구현 방식**: `Unity VideoPlayer` 기반 5초 영상 클립(슬라이더·버튼·키보드 조작). `First Frame + Last Frame` 고정 + 물리 접촉 제거(이동/시선 중심) 원칙으로 AI 영상 오류 차단.

---

## 2. 나노바나나(Nano Banana) 실무 제작 4대 원칙

1. **텍스트와 이미지의 완전 분리 (필수)**
   * 서류(보고서, 진술서, 로그) 안의 글자와 CCTV 좌상단 타임스탬프, `사건 종결` 도장은 AI로 그리지 않고 **Unity TextMeshPro(TMP) & UI 셰이더**로 렌더링합니다.
2. **CCTV 시퀀스 일관성 워크플로우 (Base + Edit 방식)**
   * 매 컷을 새로 생성하면 진열대·카운터 위치가 계속 바뀝니다.
   * 먼저 **사람이 없는 빈 편의점 천장 광각 샷(`BASE_STORE.png`) 1장**을 완벽하게 생성한 뒤, 나노바나나의 이미지 편집 기능으로 **동일 배경 위에 인물·혈흔·현금 띠지만 추가/수정**하여 컷 1, 2, 3, 5, 6을 추출합니다. (`컷 4: SIGNAL LOST`는 Unity 노이즈 UI로 처리)
3. **실존 상표명·로고 차단 네거티브 규칙**
   * 모든 프롬프트에 `no real brand logos, generic unbranded convenience store products, no readable text`를 고정 포함하여 실존 상표 노출을 원천 차단합니다.
4. **파일 네이밍 & 해상도 규격**
   * CCTV / 현장 사진 (줌인 대상): **`1920 × 1080` (PNG)** — `2.5x` 확대 시에도 선명도 유지.
   * 인물 초상화 (신분증/프로필): **`512 × 512` (PNG)**.
   * UI 아이콘: **`128 × 128` (PNG, 투명 배경)**.

---

## 3. EP 1 (MVP) 전체 2D 아트 에셋 체크리스트 (총 23종)

### 3.1. 공통 OS & UI 배경 (5종)
| 파일명 | 해상도 | 용도 및 묘사 |
| :--- | :---: | :--- |
| `UI_OS_WALLPAPER.png` | 1920×1080 | 다크 슬레이트 그레이 바탕, 중앙에 은은한 가상 경찰 엠블럼 워터마크(`K-POL OS`) |
| `UI_TITLE_DESK_BG.png` | 1920×1080 | 타이틀/브리핑 화면 전용: 비 오는 밤 어두운 형사 사무실 책상 위 켜진 모니터 일러스트 |
| `UI_DOC_PAPER_REPORT.png` | 1024×1448 | 빈 공식 경찰 보고서/진술서 스캔 용지 텍스처 (미세한 종이 질감, 글자 없음) |
| `UI_DOC_MEMO_NOTE.png` | 768×1024 | 점주 자필 메모용 누런 수첩 찢긴 종이 텍스처 (글자 없음) |
| `UI_BOARD_GRID_BG.png` | 1920×1080 | 디지털 증거판 전용 어두운 청회색 미세 격자 보드 배경 |

### 3.2. 인물 초상화 (5종 — `512 × 512`)
| 파일명 | 대상 | 표정 및 복장 포인트 |
| :--- | :--- | :--- |
| `CHAR_PARTNER_MINHO.png` | 강민호 경사 (파트너) | 30대 후반, 피곤하지만 믿음직한 눈빛, 어두운 점퍼 차림, 경찰청 신분증 배경 |
| `CHAR_EP01_VICTIM.png` | 박성훈 (48, 점주) | 성실한 40대 후반 남성, 단정한 평상복, 주민등록증/증명사진 톤 |
| `CHAR_EP01_SUSPECT_A.png` | 김철수 (36, 용의자 A) | 초췌하고 억울한 표정, 어두운 후드티, 경찰 유치장 촬영(머그샷) 톤 |
| `CHAR_EP01_SUSPECT_B.png` | 이준호 (24, 용의자 B) | 평범하고 선해 보이는 20대 초반 남성, 편의점 조끼 또는 평상복 증명사진 |
| `CHAR_EP01_SUSPECT_C.png` | 최태식 (57, 용의자 C) | 고집스럽고 다혈질 인상, 정장 재킷, 증명사진 |

### 3.3. 매장 정면 CCTV 시퀀스 (Base 1장 + 편집 5장 = 6종, `1920 × 1080`)
*(컷 4 `01:55~02:05 SIGNAL LOST`는 Unity 노이즈 UI로 구현)*
| 파일명 | 시각 | 편집 지시 (Base 이미지 기준 변경점) |
| :--- | :---: | :--- |
| `EP01_CCTV_BASE.png` | 기준 배경 | 천장 모서리 광각 부감 시점. 좌측 카운터·POS기, 중앙 통로, 우측 안쪽 닫힌 '창고 문'이 모두 보이는 빈 심야 편의점 |
| `EP01_CCTV_01.png` | `01:40:12` | 카운터 앞에서 중년 남성(C)이 점주에게 손가락질하며 언쟁. 창고 문 닫힘 |
| `EP01_CCTV_02.png` | `01:45:03` | 중년 남성(C)이 정문으로 퇴장, 점주는 우측 창고 문 쪽으로 걸어감 |
| `EP01_CCTV_03.png` | `01:50:18` | 알바생(B)이 백팩을 메고 정문으로 퇴근. **왼쪽 손목에 선명한 실버 체인 팔찌 + 우측 주머니에 투명 테이프 롤 일부 노출** |
| `EP01_CCTV_05.png` | `02:14:30` | 텅 빈 매장. 정문 유리창 밖에 검은 후드(A) 실루엣. **우측 창고 문 아래 틈으로 어두운 혈흔과 노란색 은행 현금 띠지가 이미 삐져나옴** |
| `EP01_CCTV_06.png` | `02:16:05` | 검은 후드(A)가 카운터 안쪽 금고에서 지폐를 챙김. 창고 쪽 바닥에 발자국 없음 |

### 3.4. 초동·재감식 현장 사진 (5종, `1920 × 1080`)
| 파일명 | 구분 | 화면 묘사 및 핵심 단서 포인트 |
| :--- | :---: | :--- |
| `EP01_PHOTO_INIT_STORAGE.png` | 초동 지급 | 편의점 창고 내부 전경. 열린 비상금고, 쓰러진 빨간 소화기. **우측 선반 아래 어두운 구석에 작은 은색 금속 조각이 어렴풋이 반짝임** |
| `EP01_PHOTO_INIT_COUNTER.png` | 초동 지급 | 카운터 내부 사진. 텅 빈 현금통, 바닥 지폐 몇 장, 열쇠 구멍이 있는 **잠긴 하단 개인 서랍** |
| `EP01_PHOTO_TAPE.png` | 재감식 | 창고 뒷문 문틀 자석 보안 센서 접점 클로즈업 — **투명 테이프 조각이 붙어 센서를 눌러놓은 흔적** |
| `EP01_PHOTO_BRACELET.png` | 재감식 | 창고 철제 선반 아래 바닥 클로즈업 — **끊어진 실버 체인 팔찌** (경찰 증거 번호표 `1` 옆에 배치) |
| `EP01_PHOTO_CASH_420.png` | B 원룸 | 어두운 원룸 침대 밑 열린 검은 가방 — **노란 은행 현금 띠지로 묶인 5만 원권 다발(420만 원)** |

### 3.5. 2D 지도 배경 & OS 앱 아이콘 (2종)
| 파일명 | 해상도 | 용도 |
| :--- | :---: | :--- |
| `EP01_MAP_CITY_BG.png` | 1920×1080 | 어두운 네이비/차콜 톤의 현대 서울 도심 도로망 벡터/전술 지도 배경 (장소 핀·노드는 Unity UI로 배치) |
| `UI_OS_ICONS_SHEET.png` | 1024×256 | 미니멀 다크 라인 아트 스타일 OS 앱 아이콘 7종 (문서, CCTV/포렌식, 경찰 DB, 지도, 메신저, 증거판, 최종 보고서) |

---

## 4. 최종 확정 프롬프트 템플릿 (현실 색감 인디 리소그래프 기준)

* **① `EP01_CCTV_BASE.png` (편의점 천장 광각 기준 배경 — 현실 반영 저채도 멀티컬러 리소그래프)**:
  > `High-angle wide-angle CCTV view from the ceiling corner of a cramped realistic Korean convenience store late at night. Indie risograph print and rough screenprint illustration style, bold hand-drawn ink outlines, visible coarse paper fiber texture, slight ink misregistration, matte dry ink finish. Realistic natural everyday colors rendered in a muted, slightly desaturated risograph palette—warm white overhead fluorescent lighting, off-white linoleum floor tile grid, muted teal/beige/terracotta/ochre packaging on stocked shelves, glowing glass-door beverage refrigerators and an open kimbap/bento chiller lining the back wall, microwave and hot-water dispenser corner with trash bin. On the left is a detailed checkout counter with dual POS monitors, barcode scanner, card reader, plastic bag holder, and unbranded cigarette display rack behind it. On the far right back wall is a closed grey metal utility storage door. Zero glossy AI shine, no real brand logos, no readable text, 16:9 aspect ratio.`
* **② `EP01_CCTV_CLIP_A` (5초 영상 클립 — `02:14` 김철수 침입, 물리 접촉 오류 차단)**:
  > `Static locked-off high-angle CCTV security camera footage, zero camera movement, completely frozen background. Maintain the exact hand-drawn indie risograph print illustration style, bold ink outlines, coarse paper texture, and muted realistic natural colors from the starting image. The glass entrance door on the left is ALREADY ajar and does not move. Keeping hands inside hoodie pockets with zero hand-to-object contact, the dark hooded figure walks slowly three steps forward from the entrance gap toward the empty checkout counter, pauses, and turns his head left and right. Every background object—stocked shelves, refrigerators, POS terminal, and the closed grey metal storage door on the far right with the dark red bloodstain and muted yellow blank cash strap motionless on the floor tiles beneath the gap—remains 100% rigid and static. No morphing, no flickering objects, 5 seconds.`
* **③ `EP01_PHOTO_TAPE.png` (뒷문 자석 센서 투명 테이프 클로즈업)**:
  > `Close-up police forensic evidence photograph of a white magnetic door security sensor mounted on a grey metal backdoor frame, indie risograph screenprint illustration style, bold hand-drawn ink outlines, coarse paper texture, muted realistic natural colors under cool camera flash. A piece of clear scotch tape is stuck over the magnetic contact to jam it open. Sharp focus on the tape edges, matte dry ink finish, zero glossy AI shine, no text, 16:9.`
* **④ `CHAR_PARTNER_MINHO.png` (파트너 형사 프로필)**:
  > `Portrait of a 39-year-old Korean male detective, indie risograph print illustration style, bold ink outlines, visible paper grain texture, muted realistic natural skin tones and dark olive/charcoal utility jacket over a light grey collared shirt, slightly tired but trustworthy sharp eyes, neutral muted warm-grey police ID background, matte dry ink finish, no text.`
