public class CharacterStats
{
    // Combat Stats
    public Stat MaxHealth;
    public Stat Energy;
    public Stat EnergyRegen;
    public Stat Might;
    public Stat Intelligence;
    public Stat Armor;
    public Stat Speed;
    public Stat Grit;

    // Board Stats
    public Stat Luck;
    public Stat Growth;

    public CharacterStats(CharacterSheet sheet)
    {
        MaxHealth = new Stat(sheet.maxHealth);
        Energy = new Stat(sheet.energy);
        EnergyRegen = new Stat(sheet.energyRegen);
        Might = new Stat(sheet.might);
        Intelligence = new Stat(sheet.intelligence);
        Armor = new Stat(sheet.armor);
        Speed = new Stat(sheet.speed);
        Grit = new Stat(sheet.grit);
        
        Luck = new Stat(sheet.luck);
        Growth = new Stat(sheet.growth);
    }
}