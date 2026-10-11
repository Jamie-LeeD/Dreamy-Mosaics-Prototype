using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [SerializeField] private int health = 5;
    [SerializeField] private static int maxHealth = 5;
    [SerializeField] public int destroyedTile = 0;
    [SerializeField] public int arrAllowed = 0;

    private bool playOnce = true;

    [SerializeField] private Slider healthBar;
    [SerializeField] private AudioManager audioManager;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        healthBar.maxValue = maxHealth;
        healthBar.value = health;
    }

    private void FixedUpdate()
    {
        if (destroyedTile == 4 || destroyedTile == 181 || destroyedTile == 254 || destroyedTile == 435 || destroyedTile == 567 || destroyedTile == 831 || destroyedTile == 899)
        {
            if(playOnce)
            {
                arrAllowed++;
                audioManager.PlayRightSFX();
                playOnce = false;
            }      
        }
        
        if (destroyedTile == 5 || destroyedTile == 182 || destroyedTile == 255 || destroyedTile == 436 || destroyedTile == 568 || destroyedTile == 832 || destroyedTile == 900)
        {
            playOnce = true;
        }
        if(destroyedTile == 925)
        {
            SceneManager.LoadScene("Win Condition");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public void takeDamage()
    {
        health = health - 1;
        healthBar.value = health;
        audioManager.PlayWrongSFX();
        if(health == 0)
        {
            Dead();
        }
    }

    public void Dead()
    {
        SceneManager.LoadScene("Lose Condition");
    }
}
