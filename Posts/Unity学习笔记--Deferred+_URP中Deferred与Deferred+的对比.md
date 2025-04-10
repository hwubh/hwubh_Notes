# Unity学习笔记--Deferred+渲染_URP中Deferred渲染与Deferred+渲染的对比

## 提要：
Unity6.1版本中在URP中引入了新渲染路径[**Deferred+**]()。 本文主要通过对比URP中**Deferred+**渲染和**Deferred**渲染来理解前者的实现思路。 因为本人才疏学浅，可能存在一些错误的地方，还请各位大佬斧正。

以下是正文：

## 前言
Deferred+（Plus），其思路上