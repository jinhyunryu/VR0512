# Photon 2인 멀티플레이 시작 메모

현재 프로젝트에는 Photon SDK가 아직 들어와 있지 않다. 그래서 `Assets/_Project/Scripts/Multiplayer`의 Photon 스크립트는 PUN 2가 설치됐을 때만 컴파일되도록 조건부 처리되어 있다.

## 권장 1차 목표

- `ArcadeRoom_Main`을 메인 씬으로 유지한다.
- 2명까지 같은 Photon Room에 접속한다.
- 각 플레이어는 자기 XR Rig만 직접 조작한다.
- 상대 플레이어는 카메라/컨트롤러를 가진 XR Rig가 아니라 머리와 양손 비주얼 아바타만 보이게 한다.
- 미니게임 물리는 처음에는 Master Client 또는 해당 스테이션을 시작한 플레이어 기준으로 동기화한다.

## Photon PUN 2 설치 후 할 일

1. Photon PUN 2를 프로젝트에 Import한다.
2. Photon AppId를 PhotonServerSettings에 입력한다.
3. `Resources` 폴더 아래에 네트워크 플레이어 프리팹을 둔다.
4. 네트워크 플레이어 프리팹에 `PhotonView`와 `PhotonPunXrAvatar`를 붙인다.
5. `PhotonView > Observed Components`에 `PhotonPunXrAvatar`를 넣는다.
6. `ArcadeRoom_Main`에 `MultiplayerRoot` 빈 오브젝트를 만들고 `PhotonPunArcadeRoomLauncher`를 붙인다.
7. `ArcadeMultiplayerSessionConfig` 에셋을 만들어 `Max Players`를 2로 둔다.
8. 씬에 `Player1`, `Player2` 위치용 빈 오브젝트를 만들고 `ArcadeMultiplayerSpawnPoint`를 붙인다.

## 나중에 동기화할 우선순위

- 1순위: 플레이어 머리/양손 위치
- 2순위: 버튼 입력과 게임 시작/리셋 이벤트
- 3순위: 각 게임의 점수/타이머/UI
- 4순위: 물리 오브젝트 위치 보정

물리 오브젝트 전체를 매 프레임 네트워크로 완벽히 맞추는 것은 비용과 튐 현상이 크다. 처음에는 "누가 권한을 갖고 계산할지"를 정하고, 결과만 동기화하는 방식이 안전하다.
