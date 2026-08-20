using System;
using System.Windows;
// ★【重要】ここでも「Application」と言ったら「WPFの方」を使うよう強制します
using Application = System.Windows.Application;

namespace MinecraftServerGeneratorWpf
{
    // おそらく名前空間は "QuicCraft" など、作成時のプロジェクト名になっているはずです。
    // エラーが出ないよう、class 定義の中身だけ確認してください。
    public partial class App : Application
    {
    }
}