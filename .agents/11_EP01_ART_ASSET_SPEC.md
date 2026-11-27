# 나노바나나 2D 아트 컨셉 시안 및 EP 1 에셋 명세서 (11_EP01_ART_ASSET_SPEC)

> **문서 목적**: `K-POL OS` 기반 2D 추리 게임의 비주얼 컨셉 시안 5가지를 비교 제안하고, EP 1(MVP) 구현에 필요한 전체 2D 스프라이트·CCTV·현장 사진·**사건 관련자 전원 초상화(9인 + 심문 표정 변형)** 에셋 목록(기본 27종)과 나노바나나(Nano Banana) 일관성 유지용 프롬프트 가이드를 정의합니다.

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

## 3. EP 1 (MVP) 전체 2D 아트 에셋 체크리스트 (기본 27종 + 심문 표정 변형)

### 3.1. 공통 OS & UI 배경 (5종)
| 파일명 | 해상도 | 용도 및 묘사 |
| :--- | :---: | :--- |
| `UI_OS_WALLPAPER.png` | 1920×1080 | 다크 슬레이트 그레이 바탕, 중앙에 은은한 가상 경찰 엠블럼 워터마크(`K-POL OS`) |
| `UI_TITLE_DESK_BG.png` | 1920×1080 | 타이틀/브리핑 화면 전용: 비 오는 밤 어두운 형사 사무실 책상 위 켜진 모니터 일러스트 |
| `UI_DOC_PAPER_REPORT.png` | 1024×1448 | 빈 공식 경찰 보고서/진술서 스캔 용지 텍스처 (미세한 종이 질감, 글자 없음) |
| `UI_DOC_MEMO_NOTE.png` | 768×1024 | 점주 자필 메모용 누런 수첩 찢긴 종이 텍스처 (글자 없음) |
| `UI_BOARD_GRID_BG.png` | 1920×1080 | 디지털 증거판 전용 어두운 청회색 미세 격자 보드 배경 |

### 3.2. 인물 초상화 (기본 9종 — `512 × 512` + 심문 표정 바리에이션)
> **심문 감정(`emotion`) 연출 규격**: 심문 가능 7인(`김철수·이준호·최태식·한소라·오민석·정대호·윤서진`)은 기본 초상화 1장(`_CALM.png`)을 Base로 생성한 뒤, 나노바나나 Image Edit 기능으로 **`_NERVOUS.png` (동요·식은땀·시선 회피)** 및 **`_BROKEN.png` (붕괴·격앙·실토)** 2종 변형을 추가 추출해 LLM `emotion` 태그(`calm / annoyed / nervous / panicked / broken / cooperative`)에 1:1 매핑합니다. (초기 프로토타입에서는 기본 1장 + UI 틴트/흔들림 연출로 먼저 가동 후 표정 컷을 순차 교체 가능)

| 파일명 | 대상 | 구분 | 표정 및 복장 포인트 |
| :--- | :--- | :---: | :--- |
| `CHAR_FIELD_MINHO.png` | 강민호 경사 (39, 현장수사반 반장) | 현장반 | 피곤하지만 믿음직한 눈빛, 어두운 유틸리티 점퍼 차림, 경찰청 신분증 배경 |
| `CHAR_EP01_VICTIM_PARK.png` | 박성훈 (48, 점주 / 피해자) | 심문 불가 | 성실한 40대 후반 남성, 단정한 평상복, 증명사진 톤 *(심문창 잠금 표시용)* |
| `CHAR_EP01_KIM.png` | 김철수 (36, 초동 피의자) | 심문 가능 | 초췌하고 억울한 표정, 어두운 후드티, 경찰 유치장 촬영(머그샷) 톤 |
| `CHAR_EP01_LEE.png` | 이준호 (24, 야간 알바 / 진범) | 심문 가능 | 평범하고 선해 보이는 20대 초반 휴학생, 어두운 맨투맨/편의점 조끼 |
| `CHAR_EP01_CHOI.png` | 최태식 (57, 상가 건물주) | 심문 가능 | 고집스럽고 다혈질 인상, 짙은 정장 재킷, 굳은 입매 |
| `CHAR_EP01_HAN.png` | 한소라 (22, 주간 알바) | 심문 가능 | 다소 위축되고 눈치 보는 20대 초반 대학생, 니트 가디건·단정한 단발/묶음머리 |
| `CHAR_EP01_OH.png` | 오민석 (41, 맞은편 '별밤호프' 사장) | 심문 가능 | 동네 자영업자 인상, 짙은 앞치마·편한 셔츠, 눈썰미가 날카롭지만 귀찮은 듯한 눈빛 |
| `CHAR_EP01_JUNG.png` | 정대호 (33, 한빛물류 배송기사) | 심문 가능 | 새벽 배송에 지친 피곤한 인상, 무지 작업복 점퍼·야구모자 착용, 사무적 표정 |
| `CHAR_EP01_YOON.png` | 윤서진 (29, 최초 발견·112 신고자) | 심문 가능 | 야근 후 지치고 충격받은 20대 후반 IT 디자이너, 안경·어두운 코트/머플러 |

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
| `EP01_PHOTO_TAPE.png` | 재감식 | 창고 뒷문 문틀 자석 보안 센서 접점 클로즈업 — **소형 자석을 붙였다 급히 떼어내다 문틀 센서 본체에 찢겨 남은 투명 테이프 조각과 긁힘 흔적** |
| `EP01_PHOTO_BRACELET.png` | 재감식 | 창고 철제 선반 아래 바닥 클로즈업 — **끊어진 실버 체인 팔찌** (경찰 증거 번호표 `1` 옆에 배치) |
| `EP01_PHOTO_CASH_420.png` | B 원룸 | 어두운 원룸 침대 밑 열린 검은 가방 — **노란 은행 현금 띠지로 묶인 5만 원권 다발(420만 원)** |

### 3.5. 건물 평면도(블루프린트) & OS 앱 아이콘 (2종)
| 파일명 | 해상도 | 용도 |
| :--- | :---: | :--- |
| `EP01_BLUEPRINT_STORE.png` | 1920×1080 | 편의점 매장(정문·카운터·진열대·냉장고) + 창고 백룸(책상/NVR·두꺼비집·금고·선반·뒷문 자석 센서) + 상가 뒤편 우회 골목이 한눈에 보이는 2D 건축 평면도(블루프린트) |
| `UI_OS_ICONS_SHEET.png` | 1024×256 | 미니멀 다크 라인 아트 스타일 OS 앱 아이콘 6종 (파일 탐색기, 디지털 증거판, 경찰 DB, 현장수사반, 용의자 심문, 최종 보고서) |

* **⑤ `EP01_BLUEPRINT_STORE.png` (건물 평면도 / 블루프린트 프롬프트 — 스타일 크롭 참조)**:
  > `Clean top-down 2D architectural floorplan blueprint of a small Korean convenience store and attached backroom storage office, showing the exact spatial layout: front sliding glass entrance on the upper-left, L-shaped checkout counter with POS monitors along the left wall, central double-sided snack shelving aisle, beverage coolers along the upper-right back wall, an interior steel door leading right into a cramped rectangular backroom (containing a small desk with CCTV monitor/NVR and safe on its upper/left wall, steel shelving rack along its right wall, and a rear alley exit door on its back wall), plus the narrow exterior service alley wrapping from the front street around to the rear alley door. Indie risograph print and technical ink architectural drawing style matching the attached style reference: crisp hand-drawn dark charcoal ink blueprint lines, muted slate-teal structural walls, aged newsprint cream paper background with subtle technical grid and fine ink speckles, zero 3D perspective, strictly orthographic top-down 2D plan, no readable text, no real brand logos. 16:9 aspect ratio.`

---

## 4. 최종 확정 프롬프트 템플릿 (현실 색감 인디 리소그래프 기준)

* **① `EP01_CCTV_BASE.png` (편의점 천장 광각 최종 마스터 배경 — 무참조 단독 생성용 서늘한 심야 느와르 리소그래프)**:
  > `[MOOD, ART CONCEPT & HIGH-DENSITY QUALITY — EERIE NOIR RISOGRAPH ILLUSTRATION]`
  > `Eerie, tense, unsettling late-night Korean crime-noir atmosphere: stark, lonely fluorescent store interior isolated against a pitch-black speckled night outside. High-craft hand-inked indie graphic novel and distressed vintage risograph screenprint illustration. Bold, expressive, slightly gritty hand-drawn black ink pen outlines with rich weight variation, crisp interior detail lines, and punchy deep charcoal-black ink shadow accents tucked beneath shelves and counter recesses. Pronounced coarse aged newsprint paper grain, visibly distressed risograph screenprint texture, authentic slight ink-plate misregistration, and generous scattered black-and-white ink speckles/splatter across floor tiles, walls, ceiling, and dark glass windows. Render ALL interior floor and wall shadows using the exact same matte risograph stipple/grain ink texture as the dark storefront glass—kept at a balanced medium-light muted cool slate-grey density so floor tile grout lines remain crisp and readable (zero scratchy parallel pen-hatching lines, zero muddy black blobs). Strict 4-ink palette: desaturated cold slate-teal (#5E787C, #748F94), aged newsprint cream (#E6DFD1, #EFECE4), muted terracotta/dusty ochre-brown (#B58263, #C79F79), and deep matte charcoal-black (#1C2022).`
  > `[STEEP CCTV CAMERA & COMPACT PROPORTIONS]`
  > `Steep high-angle CCTV security camera view mounted high in the front-left ceiling corner, pitched sharply downward (~40 degrees) with subtle wide-angle security lens distortion across a compact, realistically packed neighborhood Korean convenience store (approx. 6.5m wide by 5m deep), creating voyeuristic surveillance tension. Tight, functional 2-tile-wide walkways—zero wasted empty floor corners, zero obstructed doors.`
  > `[COMPLETE 6-ZONE LAYOUT]`
  > `1) UPPER-LEFT STOREFRONT: Manual single-leaf hinged clear glass entrance door in a matte slate-teal aluminum frame with visible metal hinges on its LEFT edge, a prominent hydraulic overhead door-closer arm at the upper-left, and a push/pull bar handle on the right. Mounted directly onto the inside surface of the MOVING GLASS DOOR PANE itself—inside the upper-right glass area, below the top horizontal door rail (NOT on the outer doorframe)—is a small vintage metal hanging door bell on a suction-cup hook. Flat thin rectangular matte charcoal-teal entrance floor mat immediately inside the doorway; small umbrella stand beside the frame; narrow slate-teal window-side eating counter with three backless stools along the glass window outside the door swing. Pitch-black speckled night street outside.`
  > `2) LEFT TO LOWER-CENTER FOREGROUND: Extended L-shaped matte slate-teal checkout counter with ONLY ONE back-to-back dual POS monitor set at the corner bend (no third monitor), receipt printer, card reader, corded barcode scanner, open blank notebook, and pen. On the extended lower-center counter section sits a compact Korean convenience store heated fried-chicken warmer showcase oriented correctly: curved glass display front facing UPPER-RIGHT toward the customer aisle, and clerk-facing rear side (facing LOWER-LEFT toward the camera) showing flat sliding clear glass access doors with small handles revealing warm-lit metal trays of unbranded muted golden-ochre fried chicken pieces and skewers. Plain matte slate-teal clerk storage cabinets under the counter; tall unbranded cigarette rack and blank off-white wall poster on the left wall behind.`
  > `3) CENTER SALES FLOOR: Two snug chest-height parallel double-sided matte slate-teal shelving aisles stocked with unbranded muted terracotta, ochre, and cream snack pouches, cup noodles, and boxes, separated by a crisp 2-tile walkway, with a compact rectangular glass-top ice cream chest freezer flush against the right-near endcap of the front gondola.`
  > `4) UPPER-CENTER BACK WALL: A glowing 3-door glass beverage refrigerator (rows of unbranded cans and bottles) joined seamlessly on its right by a matching tall glass-door liquor/alcohol cooler (rows of unbranded pale muted sage-teal glass soju bottles, muted terracotta-brown beer bottles, and beer cans). Directly in front of all refrigerator doors runs a clean, straight 2-tile-wide unobstructed walkway.`
  > `5) RIGHT BACK WALL: Compact matte slate-teal counter with microwave oven, coffee/hot-water dispenser, stacked paper cups on top, plain empty cream plaster wall above (no food shelves above microwave), and grey swing-top trash bin beside it. On the far-right back wall: wall light switch and a closed matte slate-teal painted steel backroom utility door with horizontal blank nameplate slot, right-side hinges, and round metal doorknob on its left edge, with clean unobstructed floor tiles in front.`
  > `6) EXTREME FAR-RIGHT FOREGROUND WALL: Tall open-front matte slate-teal refrigerated convenience-food chiller cropped naturally into the far-right foreground edge, softly lit inside and stocked with sealed rectangular bento lunchboxes, individually wrapped triangular sandwiches, chilled plastic coffee cups with lids, milk cartons, and packaged convenience meals (zero exposed sushi or giant kimbap rolls).`
  > `[CEILING, LIGHTING & NEGATIVES]`
  > `Low grid ceiling tiles with parallel rows of warm-white rectangular overhead fluorescent tubes casting stark, slightly cold late-night fluorescent illumination with tense noir contrast. Strict negatives: no people, no exposed kimbap/sushi rolls, no scratchy pen-hatching shadows, no muddy black blobs, no empty dead floor corners, no extra third POS monitor, no 3D CGI rendering, no glossy plastic shine, no soft airbrush gradients, no photorealistic lens blur, no anime shading, no floating objects, no real brand logos, no readable text or garbled letters. 16:9 aspect ratio.`
* **② `EP01_CCTV_CLIP_A` (5초 영상 클립 — `02:14` 김철수 침입, 물리 접촉 오류 차단)**:
  > `Static locked-off high-angle CCTV security camera footage, zero camera movement, completely frozen background. Maintain the exact hand-drawn indie risograph print illustration style, bold ink outlines, coarse paper texture, and muted realistic natural colors from the starting image. The glass entrance door on the left is ALREADY ajar and does not move. Keeping hands inside hoodie pockets with zero hand-to-object contact, the dark hooded figure walks slowly three steps forward from the entrance gap toward the empty checkout counter, pauses, and turns his head left and right. Every background object—stocked shelves, refrigerators, POS terminal, and the closed grey metal storage door on the far right with the dark red bloodstain and muted yellow blank cash strap motionless on the floor tiles beneath the gap—remains 100% rigid and static. No morphing, no flickering objects, 5 seconds.`
* **③ `EP01_PHOTO_TAPE.png` (뒷문 자석 센서 투명 테이프 클로즈업)**:
  > `Close-up police forensic evidence photograph of a white magnetic door security sensor mounted on a grey metal backdoor frame, indie risograph screenprint illustration style, bold hand-drawn ink outlines, coarse paper texture, muted realistic natural colors under cool camera flash. A piece of clear scotch tape is stuck over the magnetic contact to jam it open. Sharp focus on the tape edges, matte dry ink finish, zero glossy AI shine, no text, 16:9.`
* **④ `CHAR_FIELD_MINHO.png` (현장수사반 반장 강민호 경사 프로필)**:
  > `Portrait of a 39-year-old Korean male detective, indie risograph print illustration style, bold ink outlines, visible paper grain texture, muted realistic natural skin tones and dark olive/charcoal utility jacket over a light grey collared shirt, slightly tired but trustworthy sharp eyes, neutral muted warm-grey police ID background, matte dry ink finish, no text.`
* **⑤ 신규 심문 대상 4인 공통 초상화 베이스 템플릿 (`HAN / OH / JUNG / YOON` 공통 스타일 앵커)**:
  > `Portrait of a {age}-year-old Korean {gender} {role_clothing}, {expression}, indie risograph screenprint illustration style matching CHAR_FIELD_MINHO, bold hand-drawn dark ink outlines, coarse newsprint paper grain texture, muted realistic natural skin and clothing tones, neutral desaturated slate-grey interrogation room / ID backdrop, matte dry ink finish, zero glossy AI shine, no readable text, 1:1 square aspect ratio.`
