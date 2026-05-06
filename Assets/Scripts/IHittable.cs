public interface IHittable
{
    // Любой класс, у которого есть этот интерфейс, обязан иметь метод OnHit()
    void OnHit(bool isHeavyAttack = false);
}
