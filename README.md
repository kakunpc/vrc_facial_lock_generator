# vrc_facial_lock_generator

# 概要

VRChat向けのアバターに表情固定ギミックを自動生成するEditor拡張です。

bd_さまの作成した[ModularAvatar](https://modular-avatar.nadena.dev/ja/)を利用してセットアップするため、元々のFxレイヤーを書き換えることなく利用できます。  
不要になった際も1つGameObjectを削除するだけで完了できます。

# インストール

前提条件として、[ModularAvatar](https://modular-avatar.nadena.dev/ja/) をインストールしている必要があります。  
ModularAvatarのインストール方法は[こちら](https://modular-avatar.nadena.dev/ja/)を参照してください。

https://kakunpc.github.io/kakunpc_vpm/ を開き、「Add to VCC」をクリックします。

クリックするとVCCが起動しAdd Repositoryが表示されます。問題なければ「I Understand,Add Repository」をクリックしてください。（もしVCCが起動しない、インストール画面が出ないなど発生した場合は、VCCのインストールかバージョンアップを行ってください。）　　
![](https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/2f3dae16-e7e3-4a40-b19f-daaa3530315d)

追加できたら、プロジェクトの「Manage Project」をクリックします。

「FacialLockGenerator」を見つけて右側の＋ボタンをクリックします。  
![image](https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/5670260b-61e7-4dcd-9843-4165f0252716)

# 使い方

## 生成する

プロジェクトを開き、アバターをシーンに配置します。

アバターのヒエラルキーを選択し、右クリックメニューから「Setup FacialLockGenerator」を選択します。  
<img width="584" alt="image" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/9dfbeac2-9e6e-4699-9ee2-7d82c515ba36">

セットアップ用のウィンドウが出てくるので、＋ボタンをクリックして「アニメーションクリップから追加」を選択します。  
<img width="654" alt="スクリーンショット 2023-06-26 15 44 38" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/978e01f6-edef-4e20-ab2c-d908a743bad9">

使用したい表情のアニメーションにチェックを入れて「追加」を押します。  
<img width="370" alt="スクリーンショット 2023-06-26 15 44 57" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/778ad93c-b77c-456b-8adc-3b04970d9594">

追加されたら表示名の変更と並べかえをします。  
<img width="403" alt="image" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/e12b8221-3cf0-4e91-92de-1bb280cffcd4">

表情変更のアニメーションが存在しない場合はブレンドシェイプから追加することもできます（上級者向け）  
<img width="407" alt="image" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/670b981a-3988-465d-b69e-8571ef716b53">

作成を押したら自動でセットアップされPrefabが生成されます。  
<img width="1221" alt="image" src="https://github.com/kakunpc/vrc_facial_lock_generator/assets/15257475/e76a889b-8418-4acd-8d7d-f84e252ab2ca">

最後にいつも通りアバターをアップロードして完了です。


## 削除する

ヒエラルキーから「MA_FaceLocker_{アバター名}」削除したら完了です。

## ギミックを使い回す

同じアバターであれば、生成された表情変更ギミックを使い回すことができます。（毎回生成する必要はありません）  
ヒエラルキーから「MA_FaceLocker_{アバター名}」をコピーするか、Projectビューから「kakunvr/FacialLockGenerator/Generated/日付_アバター名/MA_FaceLocker_{アバター名}.prefab」を探して、アバターに配置してください。

# ライセンスに関して

このEditor拡張はMITライセンスで公開しています。  

[LICENCE](./LICENCE)

また、本スクリプトを利用して生成したアセットに関しての利用は特に制限していません。  
ご利用になるアバター本来の利用規約に従ってください。  
もちろん、アバター制作者さんもアバターに含めて配布・販売することができます。  

# 使用したアセット

スクリーンショットの画像に、かぷちやのぶーす様の【オリジナル３Dモデル】ととちゃんを使用させていただきました。  
https://booth.pm/ja/items/4639608
