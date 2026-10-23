using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("GameObject")]
    [SerializeField] private GameObject[] weapons;
    [SerializeField] private GameObject[] grenades;
    [SerializeField] private GameObject grenadeObj;
    [SerializeField] private GameObject spawnEnemy;

    [Header("Options")]
    [SerializeField] private float speed;
    [SerializeField] private float jumpPower;
    [SerializeField] private bool[] hasWeapons;

    [Header("Item")]
    [SerializeField] private int ammo;
    [SerializeField] private int coin;
    [SerializeField] private int health;
    [SerializeField] private int score;
    [SerializeField] private int hasGrenades;
    
    [Header("ItemOptions")]
    [SerializeField] private int maxAmmo;
    [SerializeField] private int maxCoin;
    [SerializeField] private int maxHealth;
    [SerializeField] private int maxHasGrenades;


    [Header("Components")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private InputAction moveAction;
    [SerializeField] private GameManager manager;
    [SerializeField] private AudioSource jumpSound;

    private float velocity;
    private float baseSpeed; // 원래 속도 저장용
    private float fireDelay; // 공격 딜레이
    private string subMachineGunName;
    private float m_MvDelay = 0.0f;


    private bool isWalk;
    private bool isRun;
    private bool isJump;
    private bool isDodge;
    private bool isSwap;
    private bool keepMovingAfterDodge; // 회피를 시작 후 끝날 때 까지 플래그 유지
    private bool keepMovingAfterJump; // 점프가 시작 하고 끝날 때 까지 플래그 유지
    private bool isFireReady; // 근접 공격 준비
    private bool isHoldingAttack; // SubMachineGun일 경우, 꾹 눌렀을 때 계속 발사 되는 변수.
    private bool isReload; // 장전을 할것인지?
    private bool isAttack;
    private bool isDamage; // 플레이어가 몬스터에게 부딛혔을 때 잠깐의 무적타임을 주기 위한 변수.
    //private bool isBorder; // 벽에 부딛히고 있는가?
    private bool isShop; // 상점을 열고 있는가?
    private bool isDead;
    private bool isPickedUp;

    private Vector3 rotation;
    private Vector3 rotation_value; // 행동 후 방향키 변경이 반영되지 않는 버그 수정을 위한 변수
    private Vector3 dodgeRotation;
    private Vector3 dodgeMoveDir; // 회피동작이 끝날 때 까지 이동에 사용될 벡터
    private Vector3 jumpMoveDir; // 점프동작이 끝날 때 까지 이동에 사용될 벡터


    private Animator animator;
    private GameObject nearObject;
    private Weapon equipWeapon;
    private int equipWeaponIndex = -1;
    private MeshRenderer[] meshs;

    public int Coin => coin;
    public int Score => score;
    public int Health => health;
    public int MaxHealth => maxHealth;
    public Weapon EquipWeapon => equipWeapon;
    public bool[] HasWeapons => hasWeapons;
    public int HasGrenades => hasGrenades;


    public int Ammo => ammo;
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(); // 자식 오브젝트의 첫 번째 컴포넌트를 가져옴.
        meshs = GetComponentsInChildren<MeshRenderer>(); // 자식 오브젝트의 컴포넌트들을 가져옴.
        health = maxHealth;
        PlayerPrefs.SetInt("MaxScore", 112500);
    }

    void FixedUpdate()
    {
        fireDelay += Time.deltaTime;
        baseSpeed = speed;
        
        if(!isDead)
            Move();
        else
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (Mouse.current.leftButton.isPressed && isHoldingAttack)
        {
            StartCoroutine(AttackCoRouine());
        }

        UpdateMouseLook();
    }

    // 날개 있는 아이템 추가시 if문 해제.
    public void Move()
    {
        if(0.0f < m_MvDelay)
        {
            m_MvDelay -= Time.deltaTime;
            return;
        }

        // 공격 중에는 이동하지 않음
        if ((isFireReady && !isJump && !isDodge))
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (isDodge && keepMovingAfterDodge)
        {
            // 키보드를 떼도 dodgeMoveDir 로 이동
            float moveSpeed = baseSpeed * Time.deltaTime;
            rb.linearVelocity = new Vector3(dodgeMoveDir.x * moveSpeed,
                                            rb.linearVelocity.y,
                                            dodgeMoveDir.y * moveSpeed);
            transform.LookAt(transform.position + new Vector3(dodgeMoveDir.x, 0f, dodgeMoveDir.y));
        }
        else if (isJump && keepMovingAfterJump)
        {
            // 키보드를 떼도 jumpMoveDir 로 이동
            float moveSpeed = baseSpeed * Time.deltaTime;
            rb.linearVelocity = new Vector3(jumpMoveDir.x * moveSpeed,
                                            rb.linearVelocity.y,
                                            jumpMoveDir.y * moveSpeed);
            transform.LookAt(transform.position + new Vector3(jumpMoveDir.x, 0f, jumpMoveDir.y));
        }
        else
        {
            Walking();
        }        
    }

    public void Walking()
    {
        velocity = isWalk ? baseSpeed * 0.3f * Time.deltaTime : baseSpeed * Time.deltaTime;
        rb.linearVelocity = new Vector3(rotation.x * velocity, rb.linearVelocity.y , rotation.y * velocity);
        transform.LookAt(transform.position + new Vector3(rotation.x, 0f, rotation.y));
    }

    public void UpdateMouseLook()
    {
        // 마우스를 찍은 방향으로 공격 할때 회전
        if (Mouse.current.leftButton.isPressed && !isDodge && !isDead)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit rayHit;
            if(Physics.Raycast(ray, out rayHit, 100))
            {
                Vector3 nextVec = rayHit.point - transform.position;
                nextVec.y = 0f;
                transform.LookAt(transform.position + nextVec);
            }
        }
    }

    // 방향키를 눌렀을 때 실행되는 메서드
    public void PlayerMove(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead)
        {
            //if (isFireReady && !isJump && !isDodge)
            //    return;

            rotation = context.ReadValue<Vector2>().normalized;
            rotation_value = rotation;

            // 만약에 회피가 동작이 끝났다면, 방향 입력값 다시 주기.
            if (isDodge)
                rotation = dodgeRotation;
            isRun = true;
        }
        else if (context.canceled)
        {
            isRun = false;
            rotation = Vector3.zero;
            rotation_value = Vector3.zero;
        }
        animator.SetBool("IsRun", isRun);
    }

    // 왼쪽 쉬프트키를 눌렀을때 실행되는 메서드
    public void PlayerWalk(InputAction.CallbackContext context)
    {
        if (isDead)
            return;
        if (context.performed)
        {
            isWalk = true;
        }
        else if (context.canceled)
        {
            isWalk = false;
        }
        animator.SetBool("IsWalk", isWalk);
    }

    // 스페이스바를 눌렀을 때 실행되는 점프 메서드
    public void Jumb(InputAction.CallbackContext context)
    {
        if (context.performed && /*rotation == Vector3.zero &&*/ !isJump && !isDodge && !isSwap && !isAttack && !isDead)
        {
            rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
            isJump = true;
            animator.SetBool("IsJump", isJump);
            animator.SetTrigger("DoJump");
            keepMovingAfterJump = true;
            jumpMoveDir = rotation;

            //jumpSound.Play();
        }
    }

    // 컨트롤키를 눌렀을 때 실행되는 회피 메서드
    public void Dodge(InputAction.CallbackContext context)
    {
        if (context.performed && rotation != Vector3.zero && !isJump && !isDodge && !isSwap && !isFireReady && !isDead)
        {
            // 회피 시작 시 현재 입력 방향을 그대로 저장
            dodgeMoveDir = rotation;
            // 만약에, 회피중이라면, 지금 보는 방향 그대로 직진.
            dodgeRotation = rotation;
            speed *= 2;
            isDodge = true;
            animator.SetTrigger("DoDodge");

            Invoke("DodgeOut", 0.5f); // 회피가 끝났을 때 수행되는 함수.
        }
    }

    // 아이템을 획득 하는 키
    public void Interaction(InputAction.CallbackContext context)
    {
        if (context.performed && nearObject != null && !isJump && !isDead) // 점프 하고있는 상태일 때는 아이템 획득 불가.
        {
            if (nearObject.tag == "Weapon")
            {
                Item item = nearObject.GetComponent<Item>();
                int weaponIndex = item.GetValue();
                hasWeapons[weaponIndex] = true;

                ItemObjectPool.ReturnItem(nearObject, weaponIndex, true);

                nearObject = null;
            }
            else if (nearObject.tag == "Shop")
            {
                Shop shop = nearObject.GetComponent<Shop>();
                shop.Enter(this);
                isShop = true;
            }
        }
    }

    public void SwapWeapon(int weaponIndex, InputAction.CallbackContext context)
    {
        if (context.performed && !isJump && !isDodge && !isDead)
        {
            // 만약에 이미 무기가 들려있다면, 이전무기 비활성화 이후 활성화
            if (equipWeapon != null)
                equipWeapon.gameObject.SetActive(false);
            equipWeapon = weapons[weaponIndex].GetComponent<Weapon>();
            equipWeaponIndex = weaponIndex;

            weapons[weaponIndex].SetActive(true);

            animator.SetTrigger("DoSwap");

            isSwap = true;

            Invoke("SwapOut", 0.4f); // Swap이 끝났을 때 수행되는 함수.
        }
    }
    // ---- Input System 바인딩용 ----
    public void SwapKey0(InputAction.CallbackContext context)
    {
        int weaponIndex = 0;
        if (!hasWeapons[weaponIndex] || equipWeaponIndex == weaponIndex)
            return;
        SwapWeapon(weaponIndex, context);
    }
    public void SwapKey1(InputAction.CallbackContext context)
    {
        int weaponIndex = 1;
        if (!hasWeapons[weaponIndex] || equipWeaponIndex == weaponIndex)
            return;
        SwapWeapon(weaponIndex, context);
    }
    public void SwapKey2(InputAction.CallbackContext context)
    {
        int weaponIndex = 2;
        if (!hasWeapons[weaponIndex] || equipWeaponIndex == weaponIndex)
            return;
        SwapWeapon(weaponIndex, context);
        subMachineGunName = weapons[weaponIndex].name;
    }
    public void Attack(InputAction.CallbackContext context)
    {
        if (equipWeapon == null)
            return;
        if (context.performed && equipWeapon.name != subMachineGunName && !isJump && !isShop && !isDead)
        {
            StartCoroutine(AttackCoRouine());
            isHoldingAttack = false;
            isAttack = true;
        }
        // 만약에 SubMachineGun 이라면, 마우스를 꾹 눌렀을 때 계속 발사 되도록 구현하기.
        else if (context.performed && equipWeapon.name == subMachineGunName && !isJump && !isShop && !isDead)
        {
            isHoldingAttack = true;
            isAttack = true;
        }
        else if (context.canceled)
        {
            isHoldingAttack = false;
            isAttack = false;
        }
    }

    public void GrenadeAttack(InputAction.CallbackContext context)
    {
        if (context.performed && hasGrenades == 0)
            return;
        else if (context.performed && !isReload && !isSwap && !isDead)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, 100))
            {
                Vector3 nextVec = rayHit.point - transform.position;
                nextVec.y = 10f;

                //Grenade obj = Instantiate(grenadeObj, transform.position, transform.rotation);
                GameObject obj = ThrowGrenadeObjectPool.GetThrowGrenade();
                obj.transform.position = transform.position;
                obj.transform.rotation = transform.rotation;

                Rigidbody rigidGrenade = obj.GetComponent<Rigidbody>();
                rigidGrenade.AddForce(nextVec, ForceMode.Impulse);
                rigidGrenade.AddTorque(Vector3.back * 10, ForceMode.Impulse);
                hasGrenades--;
                grenades[hasGrenades].SetActive(false);
            }
        }
    }

    private IEnumerator AttackCoRouine()
    {
        if (equipWeapon == null)
            yield break;
        isFireReady = equipWeapon.GetRate() < fireDelay;
        if (isFireReady && !isDodge && !isSwap)
        {
            yield return null;

            equipWeapon.Use();

            string setWeapon = null;
            if(equipWeapon.GetWeaponType() == Weapon.Type.Melee)
            {
                setWeapon = "DoSwing";
            }
            else if(equipWeapon.GetWeaponType() == Weapon.Type.Range && equipWeapon.CurAmmo > 0)
            {
                setWeapon = "DoShot";
            }
            animator.SetTrigger(setWeapon);
            //animator.SetTrigger(equipWeapon.GetWeaponType() == Weapon.Type.Melee ? "DoSwing" : "DoShot");
            fireDelay = 0;
            yield return new WaitForSeconds(equipWeapon.GetWaitTime());
        }
        isFireReady = false;
    }

    // 총알을 재장전 하는 메서드.
    public void ReLoad(InputAction.CallbackContext context)
    {
        if (context.performed && !isReload)
        {
            // 들린 무기가 없을 때
            if (equipWeapon == null)
            return;
            // 근접 무기가 들려 있을 때
            if (equipWeapon.GetWeaponType() == Weapon.Type.Melee)
                return;
            // 갖고 있는 총알이 하나도 없을 때
            if (ammo == 0)
                return;
            // 무기의 탄창이 최대 개수 일 때
            if (equipWeapon.IsAmmoFull())
                return;

            if(!isJump && !isDodge && !isSwap)
            {
                animator.SetTrigger("DoReload");
                isReload = true;
                Invoke("ReLoadOut", 3f);
            }
        }
    }

    // 개발자 모드 f키 눌렀을 때 바로 앞에 Enemy 소환
    public void SpawnEnemy(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // 1️. Ray를 플레이어의 시점(정면)으로 쏜다
            Ray ray = new Ray(transform.position, transform.forward);
            RaycastHit hit;

            // 2️. 거리 5 이내에 무언가가 있으면, 거기 바로 앞에 생성
            if (Physics.Raycast(ray, out hit, 5f))
            {
                // Ray에 맞은 지점 바로 앞에 스폰
                Vector3 spawnPos = hit.point - transform.forward * 0.5f; // 0.5m 뒤쪽 (겹치지 않게)
                Enemy obj = EnemyObjectPool.Instance.GetEnemy(spawnEnemy.GetComponent<Enemy>().GetEnemyType());
                obj.transform.position = spawnPos;
                obj.transform.rotation = Quaternion.identity;
                Enemy enemy = obj.GetComponent<Enemy>();
                enemy.Initialize(transform, manager);
            }
            else
            {
                // 3️. 아무것도 없으면, 플레이어 앞 거리 5 위치에 생성
                Vector3 spawnPos = transform.position + transform.forward * 5f;
                Enemy obj = EnemyObjectPool.Instance.GetEnemy(spawnEnemy.GetComponent<Enemy>().GetEnemyType());
                obj.transform.position = spawnPos;
                obj.transform.rotation = Quaternion.identity;
                Enemy enemy = obj.GetComponent<Enemy>();
                enemy.Initialize(transform, manager);
            }
        }
    }

    // 기능
    private void ReLoadOut()
    {
        int curAmmo = equipWeapon.CurAmmo;   // 현재 무기 탄창 수
        int maxAmmo = equipWeapon.MaxAmmo;   // 무기 최대 탄창 수
        int needAmmo = maxAmmo - curAmmo;    // 장전해야 할 탄약 수

        // 이미 풀탄창이면 리턴
        if (needAmmo <= 0)
        {
            equipWeapon.SetCurAmmo(maxAmmo);
            isReload = false;
            return;
        }

        // 플레이어가 가진 탄약이 부족할 경우
        if (ammo < needAmmo)
        {
            equipWeapon.SetCurAmmo(curAmmo + ammo); // 남은 탄약만큼 채움
            ammo = 0;
        }
        else
        {
            equipWeapon.SetCurAmmo(maxAmmo); // 풀탄창
            ammo -= needAmmo;
        }

        isReload = false; // 장전 끝
    }

    private void SwapOut()
    {
        isSwap = false;
    }

    private void DodgeOut()
    {
        speed *= 0.5f;
        isDodge = false;
        rotation = rotation_value;
        keepMovingAfterDodge = true;
    }


    private void OnCollisionEnter(Collision collision)
    {
        //if(collision.contacts[0].normal.y < 0.8f)
        //{
        //    isJump = false;
        //}

        //else if (collision.gameObject.CompareTag("Floor"))
        //{
        //    isJump = false;
        //    animator.SetBool("IsJump", isJump);
        //}



        foreach (ContactPoint contact in collision.contacts)
        {
            // contact.normal : 충돌한 표면의 수직 방향 벡터
            // Vector3.up 과의 각도가 작다면(즉, 평평한 바닥이라면)
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f) // 약 45도 이내의 경사만 바닥으로 인정
            {
                isJump = false;
                animator.SetBool("IsJump", false);
                return; // 하나라도 바닥이면 종료
            }
            else
            {
                isJump = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag == "Item")
        {
            Item item = other.GetComponent<Item>();
            int itemIndex = 0;
            switch (item.GetItemType())
            {
                case Item.Type.Heart:
                    itemIndex = 0;
                    health += item.GetValue();
                    if(health > maxHealth)
                        health = maxHealth;
                    break;
                case Item.Type.Ammo:
                    ammo += item.GetValue();
                    itemIndex = 1;
                    if (ammo > maxAmmo)
                        ammo = maxAmmo;
                    break;
                case Item.Type.Grenade:
                    itemIndex = 2;
                    if (hasGrenades == maxHasGrenades)
                        return;
                    grenades[hasGrenades].SetActive(true);
                    hasGrenades += item.GetValue();
                    break;
                case Item.Type.Coin:
                    coin += item.GetValue();
                    if(coin > maxCoin)
                        coin = maxCoin;
                    break;
            }
            if (item.GetItemType() == Item.Type.Coin)
                ItemObjectPool.ReturnCoin(other.gameObject, item.CoinPoolIndex);
            else
                ItemObjectPool.ReturnItem(other.gameObject, itemIndex, false);
        }
        OnHitByBullet(other);
    }


    public void OnHitByBullet(Collider other)
    {
        if(!other.CompareTag("Item") && !other.CompareTag("Shop") && !other.CompareTag("Weapon") && !other.CompareTag("Untagged"))
        {
            if (!isDamage)
            {
                Bullet enemyBullet = other.GetComponent<Bullet>();
                health -= enemyBullet.GetDamage();
                if (health <= 0)
                {
                    health = 0;
                }
                bool isBossAtk = other.name == "Boss Melee Area";
                StartCoroutine(OnDamage(other, isBossAtk));
            }

            if (other.GetComponent<Rigidbody>() != null)
            {
                switch (other.tag)
                {
                    case "EnemyBullet":
                        EnemyBulletObejctPool.Instance.ReturnEnemyCBulletPool(other.gameObject.GetComponent<Bullet>());
                        break;
                    case "BossRock":
                        EnemyBulletObejctPool.Instance.ReturnBossRockPool(other.gameObject.GetComponent<BossRock>());
                        break;
                    case "BossMissile":
                        EnemyBulletObejctPool.Instance.ReturnBossBulletPool(other.gameObject.GetComponent<BossMissile>());
                        break;
                    default:
                        Debug.Log("null");
                        break;
                }
            }
        }
    }
    public IEnumerator OnHitDamage(int damage)
    {
        isDamage = true;
        foreach (MeshRenderer mesh in meshs)
        {
            if (!mesh.CompareTag("Invisible"))
                mesh.material.color = Color.yellow;
        }

        health -= damage;

        if (health <= 0 && !isDead)
        {
            health = 0;
            onDie();
        }

        yield return new WaitForSeconds(0.5f);

        foreach (MeshRenderer mesh in meshs)
        {
            if (!mesh.CompareTag("Invisible"))
                mesh.material.color = Color.white;
        }
        isDamage = false;
    }

    public IEnumerator OnDamage(Collider cdr , bool isBossAtk)
    {
        isDamage = true;
        foreach (MeshRenderer mesh in meshs)
        {
            if(!mesh.CompareTag("Invisible"))
                mesh.material.color = Color.yellow;
        }

        

        if (isBossAtk)
        {
            rb.AddForce(transform.forward * -25, ForceMode.Impulse);
            m_MvDelay = 0.5f;
        }
        Vector3 reactVector = transform.position - cdr.transform.position;
        reactVector = reactVector.normalized;
        reactVector += Vector3.back;
        rb.AddForce(reactVector * 5, ForceMode.Impulse);

        if (health <= 0 && !isDead)
            onDie();

        yield return new WaitForSeconds(0.5f);

        if (isBossAtk)
            rb.linearVelocity = Vector3.zero;

        foreach (MeshRenderer mesh in meshs)
        {
            if (!mesh.CompareTag("Invisible"))
                mesh.material.color = Color.white;
        }
        isDamage = false;

    }

    private void onDie()
    {
        animator.SetTrigger("doDie");
        isDead = true;
        manager.GameOver();
    }

    private void OnTriggerStay(Collider other)
    {
        if(other.tag == "Weapon" || other.tag == "Shop")
        {
            nearObject = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Weapon")
        {
            nearObject = null;
        }
        else if (other.tag == "Shop")
        {
            Shop shop = nearObject.GetComponent<Shop>();
            shop.Exit();
            isShop = false;
            nearObject = null;
        }
    }




    public void SetCoin(int coin)
    {
        this.coin = coin;
    }

    internal void SetScore(int score)
    {
        this.score = score;
    }
}

---

# 이 코드 분석 — "들러붙는 버그" 처리 방식 파악 (2026-08-16)

## 이 파일의 정체

`TagOfChaos`의 `HideOrSeekPlayer`가 아니라, **다른(이전) 프로젝트의 3인칭 슈팅 게임 플레이어
컨트롤러**로 보인다 — 무기 스왑/장전/투척, 상점, 적 스폰, 코인/탄약/체력 아이템, 대미지 처리 등
술래잡기 게임과 무관한 기능이 대부분이다. 참고할 만한 부분은 **이동/점프 물리 처리 부분
(`FixedUpdate`/`Move`/`Walking`/`Jumb`/`OnCollisionEnter`)**뿐이고, 이 부분이 지금
`PlayerControllPlan.md` §22("들러붙는 버그")와 직접 관련된 내용이라 그 부분 위주로 분석했다.

## 이동/점프 처리 방식 요약

- `FixedUpdate()`에서 `Move()`를 매 프레임 호출 — 우리 `HideOrSeekPlayer.FixedUpdate()`와 구조가
  거의 동일하다(입력은 별도 콜백에서 미리 받아두고, 물리 갱신만 `FixedUpdate`에서).
- `Walking()`: `rb.linearVelocity = new Vector3(rotation.x * velocity, rb.linearVelocity.y,
  rotation.y * velocity)` — **우리 `Move()`와 완전히 같은 패턴**이다. 즉 매 물리 스텝마다 수평
  속도를 입력 방향으로 무조건 덮어쓴다. `PlayerControllPlan.md` §22.2 후보①에서 지적한
  "매 프레임 속도 강제 대입이 마찰/충돌 반응과 계속 충돌한다"는 구조적 특징이 **이 코드에도
  동일하게 존재한다.**
- `Jumb()`: `rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse)` — 우리는
  `rb.linearVelocity.y = jumpPower`로 직접 대입하는데, 여기는 `AddForce(Impulse)`를 쓴다. 질량을
  고려한 순간적 힘 적용이라는 차이는 있지만, **"들러붙는" 증상과는 관련이 없는 차이**다(수평 방향
  움직임과 무관, 수직 임펄스만 다르게 준 것).
- **접지 판정 방식이 우리와 근본적으로 다르다.** 우리(`PlayerGroundDetector.IsGrounded`)는 매
  `FixedUpdate`마다 아래로 레이캐스트를 쏴서 "지금 땅에 닿아있는가"를 질의하는 방식인데, 이 코드는
  레이캐스트를 전혀 쓰지 않고 **`OnCollisionEnter` 물리 충돌 이벤트**로 접지를 판정한다:

```csharp
private void OnCollisionEnter(Collision collision)
{
    foreach (ContactPoint contact in collision.contacts)
    {
        if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f) // 약 45도 이내의 경사만 바닥으로 인정
        {
            isJump = false;
            animator.SetBool("IsJump", false);
            return;
        }
        else
        {
            isJump = false;
        }
    }
}
```

접촉면의 법선(normal) 벡터가 `Vector3.up`과 얼마나 가까운지(내적이 0.7보다 큰지, 대략 45도 이내)를
계산해서 "평평한 바닥에 닿았다"와 "벽/경사에 부딪혔다"를 구분하려는 의도로 보인다.

## 발견한 문제점 — 그대로 가져다 쓰면 안 되는 이유

1. **`else` 분기가 사실상 의미가 없다.** 바닥이든(if) 벽이든(else) 결국 둘 다
   `isJump = false`를 실행한다 — 벽에 부딪혔을 때도 "착지"로 처리되어 버린다는 뜻이다. 각도
   판정을 굳이 나눠놓고도 결과적으로 두 분기가 동일한 동작을 하고 있어, 의도한 대로 동작하고
   있는지조차 의심스럽다.
2. **`OnCollisionEnter`는 "새로운 충돌이 시작되는 그 순간" 한 번만 호출된다** —
   `OnCollisionStay`가 아니다. 즉 한번 착지 판정을 받은 뒤 그 자리에 계속 서 있어도 이 함수는
   다시 호출되지 않는다. 이 코드에는 `OnCollisionExit`(바닥에서 벗어났을 때)도 없다. 그리고
   `Jumb()`의 점프 허용 조건은 `!isJump`뿐이고 **"지금 실제로 땅에 붙어있는가"를 확인하는 절차가
   전혀 없다** — 한 번 착지해서 `isJump=false`가 된 이후, 걸어서 낭떠러지를 넘어가도(새 충돌
   이벤트가 없으므로) `isJump`는 계속 `false`로 남아 있어 **공중에서도 점프(=위로 임펄스)가
   먹혀버리는 구멍**이 있다. 우리 쪽 `PlayerControllPlan.md` §18.2에서 정확히 이런 종류의
   "이벤트 기반 접지 판정의 허점"을 근본 원인으로 지목해서 §18에서 "점프 여부와 무관하게 매
   스텝 접지를 다시 확인하는" 방식(`groundDetector.IsGrounded()`를 매 `FixedUpdate` 호출)으로
   이미 그 문제를 해결해뒀다 — 즉 이 참고 코드의 방식은 우리가 이미 고친 문제를 오히려 다시
   끌고 들어오는 셈이다.
3. **`Walking()`의 속도 계산이 의심스럽다.** `velocity = baseSpeed * Time.deltaTime`을 그대로
   `rb.linearVelocity`에 대입한다 — `Rigidbody.linearVelocity`는 이미 "초당 이동 거리" 단위라
   Unity 물리 엔진이 스텝마다 알아서 `Time.fixedDeltaTime`을 곱해서 실제 위치에 반영한다.
   여기에 `Time.deltaTime`을 한 번 더 곱해서 대입하면 **속도값이 프레임 시간만큼 한 번 더
   줄어들어**, 이론상 극히 느리거나 프레임레이트에 따라 들쭉날쭉한 이동이 된다(대신 `baseSpeed`
   자체를 비정상적으로 크게 잡아서 눈속임으로 맞췄을 가능성이 있음). 이게 실수인지, 아니면 이
   프로젝트만의 어떤 의도(예: `Time.deltaTime`을 `FixedUpdate` 안에서 써서 사실상 `fixedDeltaTime`과
   거의 같은 값이 되는 것을 노린 것)인지는 이 파일만 보고는 확신할 수 없지만, 최소한 **우리
   `Move()`(순수하게 `rb.linearVelocity = 방향 * 속도`만 대입, `Time.deltaTime` 미적용)가 더
   교과서적으로 올바른 형태**이고, 이 참고 코드를 그대로 베끼면 오히려 새로운 버그를 들여오게
   된다.
4. **가장 결정적으로 — 이 코드에도 "매 프레임 속도 강제 대입" 패턴이 우리와 동일하게 있다는 것
   자체가, 이게 "들러붙는 버그를 해결한 코드"라는 근거가 되지 못한다는 뜻이다.** `PlayerControllPlan.md`
   §22에서 후보①로 지목한 원인(마찰 재질 미설정 + 매 프레임 속도 강제 대입이 물리 충돌 반응과
   싸우는 것)이 그대로 남아있는 코드이므로, 이 참고 프로젝트도 같은 "사물에 들러붙는" 증상을
   똑같이 겪었을 가능성이 있다(다른 게임이라 얇은 펜스/테이블 다리 같은 형태가 없어서 눈에 덜
   띄었을 수는 있음). 즉 이 파일은 **"들러붙는 버그를 어떻게 고쳤는가"에 대한 검증된 해법이
   아니라, 오히려 같은 잠재적 문제를 안고 있는 별개의 구현**으로 보인다.

## 그래도 참고할 만한 부분

`OnCollisionEnter`/`OnCollisionStay`에서 **접촉면 법선과 `Vector3.up`의 내적으로 "바닥이냐 벽이냐"를
구분하는 아이디어** 자체는 유효하고, 우리 §22.3-2(사물 위에 올라설 수 있어야 하는지 레이어로
가를지 말지 고민하던 부분)에 보완적으로 쓸 수 있다 — 예를 들어 `groundLayer` 레이어 필터링 대신
(또는 그것과 함께) `OnCollisionStay`로 "지금 닿아있는 면이 위쪽을 향한 평평한 면인가"를 같이
검사하면, 레이어를 일일이 분리하지 않고도 "사물 위는 밟고 설 수 있되 옆면에는 못 붙는다"는
동작을 더 정교하게 표현할 수 있을 것으로 보인다. 다만 이건 §22의 근본 원인(마찰 미설정)을
대체하는 해법이 아니라 **추가로 고려해볼 수 있는 보조 아이디어** 정도다.

## 결론 — 이 방향을 채택하는 게 나은가?

**아니다. 이 코드를 가져다 쓰는 것은 추천하지 않는다.**

- "들러붙는 버그를 해결한 참고 사례"로 보기 어렵다 — 오히려 같은 원인(매 프레임 속도 강제 대입,
  마찰 미설정 여부는 이 스크립트만으로는 확인 불가)을 공유하고 있고, 접지 판정 방식은 우리가 이미
  §18에서 발견해서 고친 허점(이벤트 기반 판정의 사각지대 — 걸어서 낭떠러지를 넘어가도 안 걸림)을
  그대로 갖고 있다.
- `Walking()`의 `Time.deltaTime` 이중 적용은 그대로 옮기면 새로운 이동 속도 버그를 만들 가능성이
  높다.
- 유일하게 건질 만한 아이디어(접촉 법선 각도로 바닥/벽 구분)는 "들러붙는 버그"의 직접적인 해법이
  아니라 완전히 다른 문제(사물 위에 올라설 수 있는지 여부, §22.3-2)에 보조적으로 쓸 수 있는
  부분적인 참고 자료 정도다.

`PlayerControllPlan.md` §22에 이미 정리해둔 방향(마찰 0 `PhysicMaterial`을 플레이어
`CapsuleCollider`에 지정하는 것을 1순위로 하고, 필요시 얇은 콜라이더 모서리 걸림을 추가로
조사하는 것)을 그대로 진행하는 편이 이 파일을 참고해서 방향을 바꾸는 것보다 낫다고 판단한다.
