# FAQ

## **Can I use other libraries to get the data saved using this library?**
Ultimately, yes its just data in Table Storage. You will however, have to account for JSON serialized properties.

## **Can I use multiple types in the same Table?**
If you do it right, yes. Make sure your partition and row keys don't conflict.
In this case, it is also advised that you set `createTableIfNotExists: false` and call `await CreateTableIfNotExistsAsync()` on one of them. You can call on both, as long as you call them after the previous call completed. This matters only when the table doesn't exist.

## How to use ETag, If-Match Queries .etc. ?
If you need these features, you should use the official SDK directly. This library is only for simpler use cases. If you have a feature request that you think is simple and common, please open an issue and I can consider adding the functionality.

## **Found a Bug. What do I do?**
First, take a deep breath. Open an issue with a minimum code sample that demonstrates it. I'll have a look. Better yet, PRs welcome.

## **How can I want to use an newer/older version of Newtonsoft or Table SDK?**
If there's a newer version of either of these, I'm happy to release a new version with them. I prefer not downgrading the current dependency versions. (Unless they have a known unfixed bug, or a security issue.)

Though you're welcome to fork the repo and downgrade the packages.

## **I want to use this but in an older .NET version**
You're welcome to fork the repo and downgrade.

## **I have other questions**
Open an issue. I'll have a look.
