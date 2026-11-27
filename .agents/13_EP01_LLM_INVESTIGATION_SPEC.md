# EP01 LLM 기반 현장 지시 & 용의자 심문 상세 설계서

> **문서 목적**: [01_MASTER_GDD.md](./01_MASTER_GDD.md)와 [02_EPISODE_01_SPEC.md](./02_EPISODE_01_SPEC.md), 그리고 현재 구현된 `Assets/Data/Episodes/EP01/ep01.json`을 기반으로, **단순 문서 일괄 열람 구조**를 **전담수사팀장(플레이어) ↔ 휘하 현장수사반(LLM Tool Calling) ↔ 용의자 4인(LLM 상태 기반 심문)** 구조로 전환하기 위한 완전 설계 명세입니다.  
> **핵심 아키텍처 원칙**: LLM은 **자연어 해석(Parser)과 캐릭터 연기(Actor)**만 담당하며, **사건의 진실·타임라인·증거 해금·비밀 폭로 조건은 Unity C# 상태 머신과 JSON 데이터가 100% 결정론적으로 통제**합니다.

---

## 1. 역할 재정의, 법정 시한 및 핵심 루프

### 1.1. 직책 및 조직 구조
| 구분 | 역할명 | 게임 내 권한 및 기능 |
| :--- | :--- | :--- |
| **플레이어** | **서울청 중요사건 보완수사 전담팀장 (경감)** | 일선서 초동 기록을 `K-POL OS`로 이관받아 재검토. 휘하 현장수사반에 수색·감식·영장 집행을 직접 하달하고, 용의자·참고인을 직접 소환해 대면 심문하며 최종 송치 보고서를 제출. |
| **현장 지시 대상** | **휘하 현장수사반** (대표 보고 창구: **현장반장 강민호 경사**) | 팀장의 자연어 지시를 해석해 현장 감식, 주변 탐문, 외부 시스템 조회, 영장 집행을 수행하고 결과 파일(문서·사진·로그)을 `K-POL OS`로 전송. |
| **심문 대상** | **관련자 4인** (`김철수`, `최태식`, `한소라`, `이준호`) | 조사실 원격/대면 심문 창에서 팀장의 자유 질문 및 **증거판 카드 제시**에 반응. 상태 머신에 의해 통제되는 거짓말·심리 압박·진술 번복 수행. |

### 1.2. 행동 기반 제한 시간 규칙 (대한민국 형사소송법 정합성 반영)
* **시한 명분**: 일선 초동팀이 `2026.11.18 02:40` 김철수를 긴급체포함. 대한민국 형사소송법 제200조의4(긴급체포 후 48시간 이내 구속영장 청구) 기준, 검찰 검토 및 영장실질심사 준비 시간을 제외한 **전담팀 배정 보완수사 시한을 `16시간(960분)`**으로 설정 (`2026.11.18 09:00` 개시 → `2026.11.19 01:00` 마감).
* **휘발성 골든타임**: `12시간(720분)` (`2026.11.18 21:00`까지). 이 시간 안에 NVR 포렌식(`ACT_FORENSIC_NVR`)을 완료해야 EP 2·7 연결 복선인 원본 서버 IP 및 연동 계좌 끝자리(`EV_META_EP01_FULL_IP`)가 온전히 확보됨.
* **시간 소모 원칙 (`TimeManager.cs` 기존 규칙 연동)**:
  * **시간 정지(0분)**: 문서·사진·CCTV 열람, `K-POL DB` 기본 조회, 증거판 정리, 현장반장과의 단순 의견·가설 검증 대화, 질문 문구 고민 중.
  * **시간 소모**:
    * **현장수사반 출동·영장 액션 실행**: 액션별 **30분 ~ 90분** 확정 차감 (`ep01.json`의 `submissionDeadlineMinutes: 960`, `volatileDeadlineMinutes: 720` 복원).
    * **용의자 심문 세션 1회 소환**: **60분** 차감 (세션당 자유 질문 **최대 8턴**, 증거판 카드 첨부 무제한). 세션 중 조기 종료해도 60분 소모, 턴 소진 시 재소환 필요.
  * **시간 초과 처리**: 기존 `TimeManager.cs`의 연장 수사 허용(`IsOvertime`) 유지 — 강제 게임오버 대신 송치 보류 사유서 제출 후 완주 가능(최종 평가 등급만 하락).

### 1.3. 시간 예산 밸런스 검증 (EP01)
* 전체 현장수사반 액션 13종 합계: **720분 (12시간 00분)**
* 용의자 4인 각 1회 심문 세션 합계: **240분 (4시간 00분)**
* 모든 액션 + 4인 1회 심문 총합 = **960분 (16시간 00분)** → 모든 항목을 무작정 실행하면 시한에 딱 맞고, 불필요한 재심문을 반복하면 시한을 초과하도록 설계됨. 핵심 루트만 전략적으로 좁힐 경우 **7~10시간(420~600분)** 내 클리어 가능.

---

## 2. EP01 자료 단계화 및 기존 `ep01.json` 정비 명세

현재 `Assets/Data/Episodes/EP01/ep01.json`은 문서 19종·사진 5장·CCTV 1개가 전부 `isInitial: true`로 일괄 개방되어 있습니다. 이를 **초기 이관 자료(Phase 0)**와 **현장수사반·심문 해금 자료(Phase 1~3)**로 분리합니다.

### 2.1. 기존 `ep01.json`의 3가지 구조적 결함 정비 (필수 선행 작업)
1. **`EV_DOC_SECURITY_ALIBI` 합본 분리**: 현재 세이프가드 센서 로그 + 김철수 PC방 + 최태식 대리/아파트 + 한소라 기숙사 + 이준호 원룸 출입 로그가 1개 문서에 합쳐져 있음 → 현장팀 지시 대상에 맞춰 **5개 개별 문서로 분리** (`EV_DOC_SAFEGUARD_LOG`, `EV_DOC_ALIBI_KIM_PC`, `EV_DOC_ALIBI_CHOI_DRIVER`, `EV_DOC_ALIBI_HAN_DORM`, `EV_DOC_ALIBI_LEE_ONEROOM`).
2. **`PoliceDB` 이준호 항목의 무영장 금융 스포일러 제거**: 현재 `dbEntries`의 이준호 기본 신원 조회에 *"불법 스포츠 도박 사이트 이체 및 계좌 끝자리(-9912) 50만 원 송금"*이 노출됨 → 기본 DB에는 **신원·전과·주소지·근무지**만 남기고, 금융 거래 내역은 영장 액션(`ACT_WARRANT_LEE_FINANCE`) 보상 문서 **`EV_DOC_FINANCE_LEE`**로 분리.
3. **`EV_DOC_STMT_HAN` 1차/심문 분리**: 현재 한소라 초동 진술서에 이준호 팔찌·신고 예고·몽키스패너 증언이 처음부터 전부 기재됨 → 초기에는 축소 진술본(`EV_DOC_STMT_HAN_INIT`)만 제공하고, 한소라의 거짓말(폐기 도시락·스터디카페)을 심문에서 벗겨내면 전체 증언 녹취록(`EV_DOC_STMT_HAN_FULL`)이 해금되도록 재구성.

### 2.2. Phase 0: 초기 이관 자료 (`isInitial: true`) — 총 문서 7종 + 사진 3장 + CCTV 1개
플레이어가 시작 시점에 일선 초동팀으로부터 넘겨받는 최소 자료입니다.

| 파일 ID | 유형 | 제목 | 초기 포함 이유 및 플레이어 유도 단서 |
| :--- | :--- | :--- | :--- |
| `EV_DOC_INIT_REPORT` | 문서 | [보고서] 초동 수사 보고서 | 김철수 긴급체포 경위, 흉기 후보 4종, 실버 팔찌 수거, CCTV `01:55~02:05` 누락, 뒷문 잠금 상태 |
| `EV_DOC_MEDICAL` | 문서 | [의료] 응급실 진단서 및 상처 미세물증 소견 | 피해자 마지막 말(`'사장님'`), 폭 10~12cm 원통, 적색 에나멜, 백색 분말 → 비품·감식 대조 유도 |
| `EV_DOC_BLUEPRINT_TIMES` | 문서 | [도면·동선] 건물 구조 안내 및 실측 이동 시간표 | 골목 2분, 원룸 7분, PC방 6분, 기숙사 14분, 아파트 차량 11분 → 알리바이 검증 기준표 |
| `EV_DOC_STMT_KIM` | 문서 | [진술서] 초동 피의자 조서 — 김철수 | 42만 원 절도 인정, 강도 상해 부인, PC방 로그 확인 요구 |
| `EV_DOC_STMT_LEE` | 문서 | [진술서] 초동 참고인 조서 — 이준호 | `01:57` 귀가 / 갈등 없음 / 팔찌 분실 (3중 거짓말 원문) |
| `EV_DOC_STMT_CHOI` | 문서 | [진술서] 초동 참고인 조서 — 최태식 | 월세 660만 원 언쟁, `'박 사장'` 호칭, 소주병 투기, `01:47` 대리 주장 |
| `EV_DOC_STMT_HAN_INIT` | 문서 | [진술서] 초동 참고인 1차 조서 — 한소라 *(개편)* | `"22시 교대 후 바로 기숙사에 갔고 특이사항 없었다"`는 축소 진술만 기재 |
| `EV_PHOTO_BLUEPRINT` | 사진 | [도면] 편의점·창고·뒷골목 건물 평면도 | 공간 동선 파악용 |
| `EV_PHOTO_INIT_STORAGE` | 사진 | [현장 사진] 창고 백룸 내부 전경 | 소화기·공구함·NVR 책상·뒷문 센서 위치 확인 (재감식 지시 유도) |
| `EV_PHOTO_INIT_COUNTER` | 사진 | [현장 사진] 매장 카운터 및 잠긴 개인 서랍 | 잠긴 서랍 발견 → 개방 지시 유도 |
| `EV_CCTV_EP01_03` | CCTV | [현장 CCTV] 02:14 매장 정면 | 김철수 진입 직전 창고 문틈 혈흔·노란 띠지 노출 확인 |

### 2.3. Phase 1~3: 해금 자료 (`isInitial: false`) 매핑표

| 해금 파일 ID | 유형 | 제목 | 해금 경로 (액션 / 심문) |
| :--- | :--- | :--- | :--- |
| `EV_DOC_FORENSIC_ITEMS` | 문서 | [감식] 현장 수거 물품 및 창고 뒷문 문틀 정밀 감식서 | `ACT_FORENSIC_BACKDOOR` |
| `EV_PHOTO_TAPE` | 사진 | [감식 사진] 뒷문 문틀 자석 센서 투명 테이프 조각 | `ACT_FORENSIC_BACKDOOR` |
| `EV_PHOTO_BRACELET` | 사진 | [감식 사진] 선반 아래 끊어진 실버 체인 팔찌 | `ACT_FORENSIC_BACKDOOR` |
| `EV_DOC_CCTV_NVR` | 문서 | [로그] 매장 CCTV 타임라인 및 창고 NVR 포렌식 덤프 | `ACT_FORENSIC_NVR` |
| `EV_DOC_OWNER_NOTE` | 문서 | [메모] 카운터 잠긴 서랍 속 점주 자필 수첩 & 출금 전표 | `ACT_SEARCH_COUNTER_DRAWER` |
| `EV_DOC_FIRE_INSPECTION` | 문서 | [비품] 편의점 소방시설 점검표 및 창고 비품 대장 | `ACT_SEARCH_COUNTER_DRAWER` |
| `EV_DOC_TIMECARD` | 문서 | [로그] 편의점 직원 출퇴근 단말기 기록 (최근 5일) | `ACT_AUDIT_POS_TIMECARD` |
| `EV_DOC_POS_AUDIT` | 문서 | [로그] POS 현금 결제 취소 감사 내역 (최근 3개월) | `ACT_AUDIT_POS_TIMECARD` |
| `EV_DOC_QUICK_RECEIPT` | 문서 | [영수증] 편의점 퀵서비스 수령 전표 및 문구 재고 실사표 | `ACT_CHECK_INVENTORY_QUICK` |
| `EV_DOC_WITNESS_OH` | 문서 | [목격담] 맞은편 '별밤호프' 사장 목격 진술 — 오민석 | `ACT_CANVASS_NEIGHBORS` |
| `EV_DOC_WITNESS_JUNG` | 문서 | [목격담] 새벽 물류 배송기사 진술 — 정대호 | `ACT_INTERVIEW_DELIVERY` |
| `EV_DOC_SAFEGUARD_LOG` | 문서 | [로그] 세이프가드 창고 뒷문 자석 접점 센서 기록 *(분리)* | `ACT_CHECK_SAFEGUARD` |
| `EV_DOC_ALIBI_KIM_PC` | 문서 | [로그] 김철수 인근 24시 PC방 34번 좌석 이용 기록 *(분리)* | `ACT_CHECK_ALIBI_KIM` |
| `EV_DOC_ALIBI_CHOI_DRIVER` | 문서 | [로그] 최태식 대리운전 배차 및 자택 엘리베이터 출입 기록 *(분리)* | `ACT_CHECK_ALIBI_CHOI` |
| `EV_DOC_RENT_NOTICE` | 문서 | [잡문서] 상가 월세 체납 내용증명 및 독촉 문자 | `ACT_CHECK_ALIBI_CHOI` |
| `EV_DOC_ALIBI_HAN_DORM` | 문서 | [로그] 한소라 대학 기숙사 공동현관 카드키 출입 기록 *(분리)* | `ACT_CHECK_ALIBI_HAN` |
| `EV_DOC_DAILY_NOISE` | 문서 | [잡문서] 폐기 식품 반출표·스터디카페 영수증·지구대 일지 | `ACT_CHECK_ALIBI_HAN` |
| `EV_DOC_ALIBI_LEE_ONEROOM` | 문서 | [로그] 이준호 신림동 원룸 공동현관 카드키 출입 기록 *(분리)* | `ACT_CHECK_ALIBI_LEE` |
| `EV_DOC_FINANCE_LEE` | 문서 | [영장] 이준호 금융계좌 추적 회신서 *(DB에서 분리)* | `ACT_WARRANT_LEE_FINANCE` |
| `EV_DOC_STMT_HAN_FULL` | 문서 | [심문 녹취] 한소라 추가 증언 (신고 예고·팔찌·스패너) | **한소라 심문 `Layer 2` 해금** |
| `EV_DOC_CONFESSION_LEE` | 문서 | [심문 녹취] 이준호 진술 번복 및 최종 붕괴 조서 | **이준호 심문 `Layer 2` 해금** |

### 2.4. 사진·CCTV 포렌식 히트박스(`hitboxes`) 보강 명세
현재 `ep01.json`의 사진 5장·CCTV 1개는 `hitboxes: []` 상태이므로, 캡처 카드가 심문 증거 제시 및 최종 보고서에 정상 작동하도록 아래 `evidenceId`를 매핑합니다.

| 미디어 ID | 캡처 대상 영역 | 부여할 `evidenceId` | 활용처 |
| :--- | :--- | :--- | :--- |
| `EV_CCTV_EP01_03` | `02:14` 김철수 진입 직전 창고 문틈 혈흔·노란 띠지 | `EV_TAG_BLOOD_BEFORE_A` | 김철수 소거 / 김철수 심문 `Layer 1` |
| `EV_PHOTO_INIT_STORAGE` | 창고 문 옆 빨간 분말 소화기 | `EV_WEAPON_EXTINGUISHER` | 최종 보고서(흉기) / 심문 제시 |
| `EV_PHOTO_INIT_COUNTER` | 카운터 하단 잠긴 개인 서랍 | `EV_TAG_LOCKED_DRAWER` | `ACT_SEARCH_COUNTER_DRAWER` 근거 제시 |
| `EV_PHOTO_TAPE` | 문틀 자석 센서 옆 찢긴 투명 테이프 조각 | `EV_METHOD_MAGNET_TAPE` | 이준호 심문 `Layer 2` / 최종 보고서(수법) |
| `EV_PHOTO_BRACELET` | 선반 아래 끊어진 실버 체인 팔찌 | `EV_TAG_BROKEN_BRACELET` | 이준호 심문 `Layer 1B` |

---

## 3. 현장수사반(강민호 경사) LLM 지시 시스템 설계

### 3.1. 작동 파이프라인 (Intent Routing + Tool Calling)
1. **플레이어 입력**: 현장수사반 채널에 자연어 지시 입력 + 선택적으로 증거판 카드 첨부.
   * 예: *"창고 뒷문이 잠겨 있었다는데 이상해. 문틀 센서랑 수거 물품 다시 정밀 감식하고 세이프가드 로그도 뽑아와."*
2. **LLM Tool Calling (해석기)**: 시스템이 현재 `미완료 액션 목록(actionId + 요약 설명)`만 LLM에 Tool 정의로 제공. LLM은 플레이어의 문장을 **1~2개의 허용된 `actionId`**로 매핑해 반환.
3. **Unity C# 선행 검증 (결정론적 게이트)**:
   * 이미 완료된 액션인가? → 강민호 경사가 *"이미 확보해서 파일 탐색기에 올려둔 자료입니다"* 응답 (0분 소모).
   * 영장/근거가 필요한 액션인데 필수 근거(`requiredEvidenceIds`)가 확보·첨부되지 않았는가? → 강민호 경사가 **거절 사유** 안내 (0분 소모).
   * 모든 조건 충족 → UI에 **`[지시 확정: 예상 소요 OO분]`** 확인 버튼 표시 (플레이어가 오인식 시 취소 가능).
4. **확정 실행**: 버튼 클릭 시 `TimeManager.AdvanceMinutes()` 실행 → 해당 문서·사진 해금(`UnlockDocument`/`UnlockPhoto`) → 강민호 경사의 현장 보고 메시지 출력.

### 3.2. EP01 현장수사반 액션 전체 명세 (13종 + 가설 검증 1종)

| `actionId` | 카테고리 | 소요 시간 | 자연어 매칭 의도 예시 | 필수 조건 (없으면 즉시 가능) | 해금 보상 | 거절 / 미충족 시 강민호 안내 |
| :--- | :--- | :---: | :--- | :--- | :--- | :--- |
| `ACT_FORENSIC_BACKDOOR` | `FORENSIC` | 90분 | 뒷문 문틀·자석 센서·수거 흉기·팔찌 정밀 재감식 | 없음 | `EV_DOC_FORENSIC_ITEMS`, `EV_PHOTO_TAPE`, `EV_PHOTO_BRACELET` | — |
| `ACT_FORENSIC_NVR` | `FORENSIC` | 90분 | 누락된 CCTV 10분 구간 / 창고 NVR 포렌식 복구 | 없음 | `EV_DOC_CCTV_NVR` *(12h 이내 완료 시 `EV_META_EP01_FULL_IP` 완전 확보, 초과 시 IP 일부 손상본)* | — |
| `ACT_SEARCH_COUNTER_DRAWER` | `INVESTIGATION` | 60분 | 카운터 잠긴 서랍 개방, 점주 수첩·매장 비품/소방 대장 확인 | 없음 *(카운터 사진 열람 시 추천 칩 노출)* | `EV_DOC_OWNER_NOTE`, `EV_DOC_FIRE_INSPECTION` | — |
| `ACT_AUDIT_POS_TIMECARD` | `INVESTIGATION` | 60분 | 직원 출퇴근 기록·POS 현금 취소·매출 감사 내역 확보 | 없음 | `EV_DOC_TIMECARD`, `EV_DOC_POS_AUDIT` | — |
| `ACT_CHECK_INVENTORY_QUICK` | `INVESTIGATION` | 45분 | 매장 문구류 재고 실사(테이프/자석) 또는 택배·퀵 수령 전표 확인 | 없음 | `EV_DOC_QUICK_RECEIPT` | — |
| `ACT_CANVASS_NEIGHBORS` | `INVESTIGATION` | 60분 | 맞은편 호프집 등 주변 상가 심야 목격자 탐문 | 없음 | `EV_DOC_WITNESS_OH` | — |
| `ACT_INTERVIEW_DELIVERY` | `INVESTIGATION` | 45분 | 새벽 물류 배송기사 정대호 참고인 탐문 및 납품 시간 확인 | 없음 | `EV_DOC_WITNESS_JUNG` | — |
| `ACT_CHECK_SAFEGUARD` | `EXTERNAL_AGENCY` | 45분 | 보안업체(세이프가드) 창고 뒷문 센서 로그 조회 | 없음 | `EV_DOC_SAFEGUARD_LOG` | — |
| `ACT_CHECK_ALIBI_KIM` | `EXTERNAL_AGENCY` | 30분 | 김철수 24시 PC방 로그인·퇴장 기록 확인 | 없음 | `EV_DOC_ALIBI_KIM_PC` | — |
| `ACT_CHECK_ALIBI_CHOI` | `EXTERNAL_AGENCY` | 45분 | 최태식 대리운전 배차·아파트 출입 로그·월세 내용증명 확인 | 없음 | `EV_DOC_ALIBI_CHOI_DRIVER`, `EV_DOC_RENT_NOTICE` | — |
| `ACT_CHECK_ALIBI_HAN` | `EXTERNAL_AGENCY` | 45분 | 한소라 기숙사 카드키 출입·카드 결제·폐기 반출 기록 확인 | 없음 | `EV_DOC_ALIBI_HAN_DORM`, `EV_DOC_DAILY_NOISE` | — |
| `ACT_CHECK_ALIBI_LEE` | `EXTERNAL_AGENCY` | 45분 | 이준호 신림동 원룸 공동현관 카드키 출입 로그 확인 | 없음 | `EV_DOC_ALIBI_LEE_ONEROOM` | — |
| `ACT_WARRANT_LEE_FINANCE` | `EXTERNAL_AGENCY` | 60분 | 이준호 은행 계좌 추적·금융 거래 영장 신청 및 집행 | **증거판 카드 보유 또는 첨부 필수**: `EV_MOTIVE_EMBEZZLE` *(POS 감사 로그 또는 점주 수첩)* | `EV_DOC_FINANCE_LEE` *(도박 빚 + `11/17 21:00` `-9912` 50만 원 송금)* | *"팀장님, 참고인 계좌 추적은 법원 금융영장이 필요합니다. 횡령이나 금전 갈등을 입증할 POS 기록이나 점주 메모부터 확보해 첨부해 주십시오."* |
| `ACT_VERIFY_HYPOTHESIS` | `ADVISORY` | **0분** | *"내 가설 검증해봐"*, *"지금까지 단서 정리해줘"*, *"김철수가 범인 아닐까?"* | 없음 | 없음 (단서 해금 불가) | 플레이어가 **현재까지 해금한 문서 요약만** 컨텍스트로 제공해 논리적 빈틈과 다음에 확인해 볼 방향을 제안. **미해금 문서 내용·진범 정체 절대 주입 금지.** |

### 3.3. 백지 공포 방지 UX (보조 칩)
* 자연어 입력창 위에 **동적 추천 칩(최대 3개)**을 띄워 플레이어가 클릭 후 문구를 수정하거나 바로 보낼 수 있게 지원합니다.
  * 예: `창고 뒷문·물증 재감식`, `NVR 10분 누락 복구`, `이준호 원룸 출입 조회` (이미 완료된 항목은 자동 제거).

---

## 4. 용의자 4인 LLM 심문 시스템 설계

### 4.1. 핵심 통제 메커니즘: Prompt Isolation (프롬프트 격리)
LLM이 프롬프트 탈옥이나 유도에 넘어가 비밀을 누설하는 사고를 원천 차단하기 위해 **비밀 정보는 코드가 잠금을 해제하기 전까지 LLM API 요청 본문에 아예 포함하지 않습니다.**

* **상태 변수 (용의자별 런타임 관리)**:
  * `unlockedLayers`: `HashSet<string>` (초기값: `["LAYER_0"]`)
  * `composure`: `int` (평정심 `0 ~ 100`)
  * `sessionsUsed`: `int` (소환 횟수)
  * `turnsRemainingInSession`: `int` (세션당 8턴)
* **턴 처리 순서 (결정론적 판정 → LLM 연기)**:
  1. 플레이어가 질문 텍스트 + 선택적 **증거판 카드(0~2장)**를 첨부해 전송.
  2. **Unity C# 선행 판정**:
     * 첨부된 카드의 `hiddenEvidenceId`가 해당 용의자의 미해금 레이어 `triggerEvidenceIds` 조건을 충족하는가?
     * 충족 시 → 즉시 `unlockedLayers.Add(layerId)` 실행 + `composure` 고정 차감 (`-25 ~ -40`) + 해당 레이어의 **해금된 반응 지침**을 이번 턴 LLM 시스템 프롬프트에 동적 삽입.
     * 무관한 카드를 첨부한 경우 → `composure += 10` (최대 100), LLM에 *"제시된 증거는 자신과 무관하거나 논점이 빗나갔다. 여유를 보이며 반박하라"* 지시.
     * 증거 미첨부 일반 추궁 → `unlockedLayers` 변경 불가. LLM이 질문의 날카로움에 따라 `composureDelta`를 **`-10 ~ 0` 범위 안에서만** 반환하도록 코드로 클램핑(`Mathf.Clamp`).
  3. **LLM 구조화 응답 반환**:
     * 대사(`dialogue`), 표정(`emotion`), 평정심 미세 변동(`composureDelta`), 증거판 등록 가능 발췌문(`extractableStatement`).
  4. **증거판 등록 연결**: 레이어가 새로 해금된 턴에는 심문 말풍선 옆에 **`[+ 심문 진술 증거판 등록]`** 버튼이 활성화되며, 클릭 시 코드가 해당 레이어에 매핑된 결정론적 `hiddenEvidenceId`로 카드를 생성.

### 4.2. 용의자 4인 비밀 레이어 및 반응 매트릭스

#### ① 김철수 (36세, 초동 피의자 — 무고 / 단순 절도범)
* **초기 평정심**: `60` (억울함과 분노가 섞인 거친 말투)
* **호칭 특징**: 점주를 모름 (`"가게 주인"`, `"아저씨"`)

| 레이어 | 해금 조건 (C# 판정) | LLM에 주입되는 사실·태도 지침 | 평정심 변화 / 해금 보상 |
| :--- | :--- | :--- | :--- |
| `LAYER_0` *(초기)* | 기본 개방 | 카운터 42만 원 절도는 100% 인정. 강도 상해는 결백 주장. `"폭력 전과 0건이다. 02:08까지 PC방에 있었다. 매장 안 창고 문 쪽에서 비린내가 나고 섬뜩해서 2분 만에 뛰어나왔다."` | 일반 압박 시 억울함 호소 |
| `LAYER_1` *(결정적 목격 기억)* | 아래 중 1개 첨부:<br>• `EV_TAG_BLOOD_BEFORE_A` (CCTV 문틈 혈흔·띠지)<br>• `EV_DOC_ALIBI_KIM_PC` (PC방 로그) | 자신의 결백을 팀장이 믿어준다는 안도감에 긴장이 풀리며, 초동팀이 무시했던 기억을 꺼냄:<br>**`"사실 02:14에 가게 앞에 다가갈 때, 가게 우측 골목 쪽에서 검은 백팩을 멘 젊은 남자가 신림동 원룸 쪽으로 엄청 빠르게 걸어가는 뒷모습을 얼핏 봤습니다."`** | 평정심 +15 (협조 전환)<br>→ 발췌 카드 `EV_TAG_KIM_BACKPACK_HINT` 등록 가능 (이준호 추적 단서) |

#### ② 최태식 (57세, 상가 건물주 — 1차 미끼)
* **초기 평정심**: `80` (다혈질, 권위적, 반말 섞인 격앙 톤)
* **호칭 특징**: 피해자를 항상 **`"박 사장"`**이라고 호칭 (`"사장님"` 절대 사용 금지 — 호칭 소거 단서)

| 레이어 | 해금 조건 (C# 판정) | LLM에 주입되는 사실·태도 지침 | 평정심 변화 / 해금 보상 |
| :--- | :--- | :--- | :--- |
| `LAYER_0` *(초기)* | 기본 개방 | 월세 660만 원 체납 언쟁 인정. `01:35~01:45` 방문 후 `01:47` 대리 타고 `01:58` 귀가 주장. 빈 소주병 투기 인정하되 창고 진입·폭행 강력 부인. | 호칭 `"박 사장"` 자연 노출 |
| `LAYER_1` *(언쟁 당시 관찰)* | 아래 중 1개 첨부:<br>• `EV_DOC_ALIBI_CHOI_DRIVER` (대리·엘리베이터 로그)<br>• `EV_DOC_RENT_NOTICE` (내용증명) + 폭행 전과 추궁 | 알리바이가 입증되자 기세가 누그러지거나, 억울함을 풀기 위해 `01:35~01:45` 당시 본 장면을 진술:<br>**`"내가 박 사장이랑 카운터에서 언쟁할 때, 그 야간 알바 놈(이준호)이 퇴근 준비한다면서 창고 뒷문 문틀 쪽에서 투명 테이프랑 뭐를 만지작거리며 내 눈치를 보더라."`** | 평정심 -15 (이후 협조)<br>→ 발췌 카드 `EV_TAG_CHOI_TAPE_HINT` 등록 가능 (뒷문 조작 정황 힌트) |

#### ③ 한소라 (22세, 주간 알바 — 2차 미끼이자 핵심 증인)
* **초기 평정심**: `70` (조심스럽고 불안한 대학생 말투)
* **호칭 특징**: 피해자를 **`"사장님"`**이라고 호칭 (호칭 함정 대상)

| 레이어 | 해금 조건 (C# 판정) | LLM에 주입되는 사실·태도 지침 | 평정심 변화 / 해금 보상 |
| :--- | :--- | :--- | :--- |
| `LAYER_0` *(초기)* | 기본 개방 | 폐기 도시락 반출 규정 위반이 들킬까 봐 겁먹은 상태. **`"밤 10시에 교대하고 바로 기숙사로 갔습니다. 사장님이랑 준호 오빠 사이에 별일 없었어요"`**라고 축소·회피. | 거짓말 추궁 시 눈치 보며 말끝 흐림 |
| `LAYER_1` *(소소한 거짓말 붕괴)* | 아래 중 1개 첨부:<br>• `EV_DOC_ALIBI_HAN_DORM` (`00:12` 기숙사 입실)<br>• `EV_DOC_DAILY_NOISE` (22:18 폐기 반출 / 스터디카페 영수증) | 당황하며 거짓말 실토: `"죄송합니다... 폐기 도시락이랑 삼각김밥 몰래 챙겨 나와서 23:55까지 스터디카페에 있다가 00:12에 기숙사 들어갔어요. 규정 위반이라 혼날까 봐 숨겼습니다. 새벽엔 절대 안 나갔어요."` | 평정심 `-35` (눈물/당황)<br>→ **알리바이 소거 확정**, `LAYER_2` 개방 조건 충족 |
| `LAYER_2` *(핵심 증언 개방)* | `LAYER_1` 해금 상태에서:<br>이준호·점주·팔찌·흉기 관련 질문 **또는** `EV_TAG_BROKEN_BRACELET` / `EV_WEAPON_WRENCH` 첨부 | 팀장에게 죄송함과 안도감을 느껴 숨겼던 결정적 목격 3가지를 모두 털어놓음:<br>1. 교대 직전 사장님이 창고에서 이준호에게 **`"오늘 자정까지 못 채우면 내일 아침 9시에 경찰 정식 신고한다"`**고 통보.<br>2. 교대 당시 이준호 왼손목에 **얇은 실버 체인 팔찌** 착용 확인.<br>3. 빨간 몽키스패너는 2주 전 이후 아무도 안 건드렸고, **소화기**가 창고 문 바로 안쪽에 있었음. | **`EV_DOC_STMT_HAN_FULL` 자동 해금** + 발췌 카드 `EV_MOTIVE_EMBEZZLE` / `EV_TAG_HAN_BRACELET` 등록 가능 |

#### ④ 이준호 (24세, 야간 알바 — 진범, 3중 거짓말 방어막)
* **초기 평정심**: `95` (차분하고 예의 바른 휴학생 연기)
* **호칭 특징**: 피해자를 **`"사장님"`**이라고 호칭

| 레이어 | 해금 조건 (C# 판정) | LLM에 주입되는 사실·태도 지침 | 평정심 변화 / 해금 보상 |
| :--- | :--- | :--- | :--- |
| `LAYER_0` *(초기 방어막)* | 기본 개방 | 3중 거짓말 철벽 유지: ① `01:50` 퇴근 후 바로 걸어가 `01:57` 원룸 귀가, ② 사장님과 갈등 0, ③ 실버 팔찌는 며칠 전 분실, ④ NVR·금고 전혀 모름. **증거 없는 추궁에는 차분하게 부인.** | 증거 미첨부 추궁 시 `-5` 이내 제한 |
| `LAYER_1A` *(동선 거짓말 붕괴)* | 아래 중 1개 첨부:<br>• `EV_DOC_ALIBI_LEE_ONEROOM` (`02:13:40` 원룸 태그)<br>• `EV_TAG_ALLEY_WITNESS` (`EV_DOC_WITNESS_OH` 01:51 뒷골목 목격) | `01:57` 귀가 거짓말 철회 → **2차 변명 후퇴**: `"아... 죄송합니다. 사실 머리가 복잡해서 뒷골목 쪽에서 담배 두 대 피우고 천천히 걸어가느라 02시 13분에 들어갔습니다. 창고엔 안 들어갔어요."` | 평정심 `-30` (식은땀/말더듬)<br>→ 발췌 카드 `EV_TAG_LEE_BROKEN_ALIBI` |
| `LAYER_1B` *(팔찌 거짓말 붕괴)* | **교차 제시 또는 보유 후 첨부**:<br>`EV_TAG_BROKEN_BRACELET` (창고 수거 팔찌) + `EV_TAG_HAN_BRACELET` (한소라 증언) *(단독 첨부 시에도 반박 흔들림, 둘 중 하나라도 증거판 보유 + 나머지 첨부 시 완전 해금)* | 며칠 전 분실 거짓말 철회 → **2차 변명 후퇴**: `"기억이 헷갈렸습니다. 어젯밤에 차고 있긴 했는데, 퇴근 전 창고 재고 정리하다가 선반에 걸려 끊어진 것 같습니다."` | 평정심 `-30` (눈동자 흔들림)<br>→ 발췌 카드 `EV_TAG_LEE_BROKEN_BRACELET` |
| `LAYER_1C` *(동기 거짓말 붕괴)* | 아래 중 1개 첨부:<br>• `EV_MOTIVE_EMBEZZLE` (`EV_DOC_POS_AUDIT` 또는 `EV_DOC_OWNER_NOTE`)<br>• `EV_DOC_FINANCE_LEE` (도박 이체) | 갈등 없음 거짓말 철회 → **부분 인정·폭행 부인**: `"도박 빚 때문에 POS 현금에 손댄 건 맞습니다... 하지만 사장님이 갚을 기회를 주셨는데 제가 왜 때립니까? 폭행은 김철수가 한 거 아닙니까!"` | 평정심 `-30` (격앙/방어적)<br>→ 발췌 카드 `EV_TAG_LEE_ADMIT_EMBEZZLE` |
| `LAYER_2` *(완전 붕괴 / 자백)* | **복합 조건 (모두 충족)**:<br>1. `LAYER_1A / 1B / 1C` 중 **2개 이상 해금**<br>2. 수법 물증 첨부: `EV_METHOD_MAGNET_TAPE` 또는 `EV_METHOD_NVR_WIPE` 또는 `EV_DOC_QUICK_RECEIPT` 또는 `EV_DOC_FINANCE_LEE`<br>3. 현재 `composure <= 35` | 변명 불가 상태 도달. 침묵 끝에 무너지며 자백:<br>**`"내일 아침 9시에 신고되면 정말 끝장이라... 뒷문 센서에 테이프랑 자석 붙여두고, 정문으로 퇴근한 척 골목으로 돌아 들어가 '사장님' 부르고... 죄송합니다. 돈 420만 원은 원룸 옷장 안쪽 가방에 그대로 있습니다. USB는 성수동 쪽 익명 연락처(W.A.)에서 50만 원 주고 샀습니다."`** | 평정심 → `0`<br>→ **`EV_DOC_CONFESSION_LEE` 자동 해금** (최종 송치 보고서 제출의 결정적 클라이맥스) |

---

## 5. Unity C# 및 LLM API 기술 확장 명세

기존 코드베이스([EpisodeData.cs](../Assets/Scripts/Data/Episode/EpisodeData.cs), [EpisodeRuntimeState.cs](../Assets/Scripts/Core/Episode/EpisodeRuntimeState.cs), [TimeManager.cs](../Assets/Scripts/Core/Episode/TimeManager.cs))의 데이터 드리븐 구조를 깨지 않고 아래 모듈만 추가합니다.

### 5.1. 추가할 앱 윈도우 2종 (`OSWindow` 상속)
1. **`FieldCommandController` (`Assets/Scripts/Apps/FieldCommand/`)** — 기존 `TaskbarController`에 `[현장수사반]` 버튼 추가.
   * 채팅 로그 뷰 + 자연어 입력창 + 증거판 카드 첨부 슬롯 + 동적 추천 칩(3개) + `[지시 확정 (소요 시간)]` 확인 카드.
2. **`InterrogationController` (`Assets/Scripts/Apps/Interrogation/`)** — `TaskbarController`에 `[용의자 심문]` 버튼 추가.
   * 좌측: 발견된 관련자 목록(`김철수 / 최태식 / 한소라 / 이준호`) + 소환 버튼(`-60분 / 8턴`).
   * 상단: 용의자 초상화(`emotion` 태그 연동 표정 전환) + 평정심 게이지(`0~100`) + 남은 질문 턴(`8/8`) + 현재 붕괴된 거짓말 배지.
   * 하단: 질문 입력창 + 증거판 카드 첨부 슬롯(최대 2장) + 발췌 등록 버튼.

### 5.2. LLM API Structured Output JSON 스키마

#### A. 현장수사반(강민호 경사) 응답 스키마
```json
{
  "type": "object",
  "properties": {
    "replyDialogue": { "type": "string", "description": "강민호 경사의 보고·확인·거절 대사 (한국어 존댓말)" },
    "matchedActionIds": {
      "type": "array",
      "items": { "type": "string" },
      "description": "허용된 미완료 actionId 목록 중 팀장 지시에 일치하는 ID (없거나 단순 질문이면 빈 배열)"
    },
    "isHypothesisCheck": { "type": "boolean" }
  },
  "required": ["replyDialogue", "matchedActionIds", "isHypothesisCheck"]
}
```

#### B. 용의자 심문 응답 스키마
```json
{
  "type": "object",
  "properties": {
    "dialogue": { "type": "string", "description": "용의자 캐릭터 대사 (허용된 레이어 사실만 사용, 3~5문장 이내)" },
    "emotion": { "type": "string", "enum": ["calm", "annoyed", "nervous", "panicked", "broken", "cooperative"] },
    "composureDelta": { "type": "integer", "description": "증거 미첨부 시 Unity 코드가 -10~0으로 강제 클램핑" },
    "extractableStatement": { "type": "string", "description": "증거판에 등록할 만한 핵심 진술 1줄 요약 (없으면 빈 문자열)" }
  },
  "required": ["dialogue", "emotion", "composureDelta", "extractableStatement"]
}
```

### 5.3. 비용·보안·응답 지연 대응 기준
* **예상 API 호출량 (EP01 1회 완주 기준)**:
  * 현장수사반 지시: 약 10~15회
  * 용의자 심문: 평균 2~4세션 × 5~7턴 = 약 15~25회
  * **총합 약 25~40회 호출** → 경량 고속 모델(예: Gemini Flash 계열) 사용 시 에피소드 완주당 토큰 비용이 매우 낮게 유지되며, 세션·시한 제한이 비용 폭주를 자동 차단.
* **보안 구조**:
  * **개발/프로토타입 단계**: 로컬 환경변수 또는 `StreamingAssets` 외부 무시 파일(`.env` / `PlayerPrefs` 설정창)에서 API 키 로드 (Git 커밋 금지).
  * **배포 단계**: 경량 서버리스 프록시(예: Cloudflare Workers)를 경유해 클라이언트에 API 키 미포함, 또는 데모 한정 BYOK 지원.
* **지연(Latency) UX**: 요청 중 무전 송수신 아이콘 / `"현장반장 입력 중..."` / `"이준호가 시선을 피하며 침묵하고 있습니다..."` 타이핑 연출로 1~3초 지연을 게임 분위기 요소로 흡수.

---

## 6. 구현 우선순위 (Vertical Slice 로드맵)

1. **Step 1 — 데이터 개편**: `ep01.json`의 `isInitial` 분리, `EV_DOC_SECURITY_ALIBI` 5종 분할, `PoliceDB` 이준호 금융 내역 분리(`EV_DOC_FINANCE_LEE`), `EV_DOC_STMT_HAN_INIT` 작성, 사진·CCTV `hitboxes` 채우기, 시한(`960분 / 720분`) 복원.
2. **Step 2 — LLM 코어 모듈 (`Assets/Scripts/Core/LLM/`)**: `LlmClient` (`UnityWebRequest` + Structured JSON Output), `FieldCommandPromptBuilder`, `InterrogationStateMachine` 구현 (API 키 없을 때 로컬 테스트용 `MockLlmProvider` 포함).
3. **Step 3 — UI 앱 2종 연동**: `FieldCommandController`와 `InterrogationController`를 기존 `WindowManager` / `TaskbarController`에 연결하고 증거판 카드 첨부 UX 구현.
4. **Step 4 — 플레이테스트 검증**: `한소라(거짓말 소거 → 핵심 증언 해금)` + `이준호(3중 거짓말 붕괴)` 루프의 프롬프트 격리 및 탈옥 내성 집중 테스트.
