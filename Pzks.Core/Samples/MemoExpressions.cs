namespace Pzks.Core.Samples;

public sealed record SampleExpression(string Id, string Expression, string Description = "");

/// <summary>Тестові вирази з «Пам'ятки ЛР ПЗКС».</summary>
public static class MemoExpressions
{
    public static readonly SampleExpression MemoExample = new("Приклад",
        "-a ++ b - 2v*func((t+2 -, sin(x/*2.01.2), )/8(-)**",
        "Приклад виводу з пам'ятки");

    public static readonly IReadOnlyList<SampleExpression> Lab1Single =
    [
        new("1", "*a + nb -", "Некоректний початок і завершення виразу"),
        new("2", "a ++ nb /* k -+/ g", "Множинні послідовності операторів"),
        new("3", "a^b$c - d#h + q%t + !b&(z|t)", "Невідомі оператори чи невідомі токени"),
        new("4", "x + var1 + var_2 + _var_3 + var#4 + var!5 + 6var_ + $7 + ?8", "Неправильний формат змінних"),
        new("5", "125 + 2nb - 0xAB * 0x0R + 0b010 * 0b20 + ABh * 0Rh + 010b*20b", "Неправильний формат цілочисельних констант"),
        new("6", "0.71/0.72.3 + .3 + 127.0.0.1*8. + 6.07ab - 9f.89hgt", "Помилки в форматі дробових чисел"),
        new("7", ")a+b( -(g+h)(g-k))*()) + (-b(t-2*x*(5) + A[7][2-x]", "Помилки з дужками"),
        new("8", "2(t) - f2(t) + g()/h(2, )*func(-t/q, f(4-t), -(x+2)*(y-2))", "Помилки з функціями"),
    ];

    public static readonly IReadOnlyList<SampleExpression> Lab1General =
    [
        new("З1", "/a*b**c + m)*a*b + a*c - a*smn(j*k/m + m"),
        new("З2", "-cos(-&t))/(*(*f)(127.0.0.1, \"/dev/null\", (t==0)?4more_errors:b^2) - .5"),
        new("З3", "//(*0)- an*0p(a+b)-1.000.5//6(*f(-b, 1.8-0*(2-6) %1 + (++a)/(6x^2+4x-1) + d/dt*(smn(at+q)/(4cos(at)-ht^2)"),
        new("З4", "-(-5x((int*)exp())/t - 3.14.15k/(2x^2-5x-1)*y - A[N*(i++)+j]"),
        new("З5", "-(-exp(3et/4.0.2, 2i-1)/L + )((void*)*f()) + ((i++) + (++i/(i--))/k//) + 6.000.500.5"),
        new("З6", "**f(*k, -p+1, ))2.1.1 + 1.8q((-5x ++ i)"),
        new("З7", "/.1(2x^2-5x+7)-(-i)+ (j++)/0 - )(*f)(2, 7-x, )/q + send(-(2x+7)/A[j, i], 127.0.0.1 ) + )/"),
        new("З8", "*101*1#(t-q)(t+q)//dt - (int*)f(8t, -(k/h)A[i+6.]), exp(), ))(t-k*8.00.1/.0"),
    ];

    public static readonly IReadOnlyList<SampleExpression> Lab2 =
    [
        new("М1", "a+b+c+d+e+f+g+h", "Паралельність додавання"),
        new("М2", "a-b-c-d-e-f-g-h", "Розпаралелювання віднімання"),
        new("М3", "a/b/c/d/e/f/g/h", "Розпаралелювання ділення"),
        new("М4", "a*(b-4) - 2*b*c - c*d - a*c*d/e/f/g - g-h-i-j", "Пріоритетність"),
        new("P1", "a+(b+c+d+(e+f)+g)+h", "Розкриття дужок з плюсом"),
        new("P2", "a-((b-c-d)-(e-f)-g)-h", "Розкриття дужок з мінусом"),
        new("E1", "5040/8/7/6/5/4/3/2", "= 0.125"),
        new("E2", "10-9-8-7-6-5-4-3-2-1", "= -35"),
        new("E3", "64-(32-16)-8-(4-2-1)", "= 39"),
        new("S1", "-(-i)/1.0 + 0 - 0*k*h + 2 - 4.8/2 + 1*e/2", "Спрощення"),
        new("S2", "a*2/0 + b/(b+b*0-1*b) - 1/(c*2*4.76*(1-2+1))", "Ділення на 0"),
    ];

    public static readonly IReadOnlyList<SampleExpression> Commutative =
    [
        new("К1", "a-b*c+k"),
        new("К2", "A - B*c - L*k*2 + D*t/d*y - H + Q*3 - J*(w-1)/r + P"),
        new("К3", "A-B*c-J*(d*t*j-u*t+c*r-1+w-k/q+m*(n-k*s+z*(y+u*p-y/r-5)+x+t/2))/r+P"),
        new("К4", "a + b*c + d + e*f*g + h*i + j*(k + L + m*(n-p*q+r) - s*t)"),
        new("К5", "exp(sin(a+b/2-pi)+a*cos(a*pi+b*pi/3-w+k*t)-5+log(2.72)/T-1)+2048+a+b*c+log(t-1)-2*log(Q)-8*d/dt*exp(t/2+H)-sin(a)/cos(a)"),
    ];

    public static readonly IReadOnlyList<SampleExpression> Associative =
    [
        new("А1", "a*b+a*c+b*c"),
        new("А2", "a*(b-2)+c*(b-2)"),
        new("А3", "a*b-2*a+b*c-c*2"),
        new("А4", "a/b-c/b+2/b"),
        new("А5", "a/(b-1)-c/(b-1)+2/(b-1)-t"),
        new("А6", "a-b*k+b*t-f*f*5.9+f*q+g*f*5.9-g*q-f/(d+q-w)-g/(d+q-w)"),
        new("А7", "a-b*k+b*t-b*f*f*5.9+b*f*q+b*g*f*5.9-b*g*q-b*w/p+b*y*m/p-b*y/p-x*x/(d+q-w)+3*x/(d+q-w)+3*x/(d+q-w)+3*3/(d+q-w)"),
    ];

    public static readonly IReadOnlyList<SampleExpression> Distributive =
    [
        new("Д1", "a*(b+c-1)*d"),
        new("Д2", "(a-c)*(b-k+1)"),
        new("Д3", "(1-d)/(a+b-2)/e"),
        new("Д4", "a-b*(k-t)-(f-g)*(f*5.9-q)-(f+g)/(d+q-w)"),
        new("Д5", "a-b*(k-t+(f-g)*(f*5.9-q)+(w-y*(m-1))/p)-(x-3)*(x+3)/(d+q-w)"),
    ];

    /// <summary>Приклад графа перетворень з розділу «Лаб 3 та 4» пам'ятки: (a-c)k + (b-c)t - (a-t)x.</summary>
    public static readonly SampleExpression DistributiveExample = new("Приклад", "(a-c)*k+(b-c)*t-(a-t)*x",
        "Приклад графа з пам'ятки");

    /// <summary>Тести ЛР4: Д1–Д5 і приклад з пам'ятки.</summary>
    public static IReadOnlyList<SampleExpression> Lab4 => [.. Distributive, DistributiveExample];

    /// <summary>Вирази, які за граматикою ЛР1 мають бути коректними (вхідні дані ЛР2–ЛР4).</summary>
    public static IEnumerable<SampleExpression> AllValid =>
        Lab2.Concat(Commutative).Concat(Associative).Concat(Lab4);
}
