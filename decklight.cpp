#include <iostream>
#include <stdlib.h>
#include <algorithm>
#include <stdio.h>
#include <time.h>

using namespace std;


class Card
{
	public:
	string Suit;
	string Denomination;
	int Randsort;

	Card(string s, string d)
	{
		this->Suit =s;
		this->Denomination=d;
		this->Randsort = rand() % 1000;
	}
	string GetCardName()
	{
		return this->Suit+this->Denomination;
	}
};

bool CompareCards(Card* c1, Card* c2)
{
	return c1->Randsort < c2->Randsort;
}

class Deck
{
	public:
	Card* Cards [52];

	
	void createDeck()
	{
		string suits[4] = {"♠","♡","♢","♣"};
		string dn [13] = {"A","2","3","4","5","6","7","8","9","10","J","Q","K"};

		for(int i=0;i<4;i++)
		{
			for(int j=1;j<=13;j++)
			{
				int cardi=(i*13)+j-1;
				Cards[cardi] = new Card(suits[i], dn[j-1]);

			}
		}
		std::sort(Cards,Cards+52,CompareCards);
	}
};


int main()
{
	srand(time(NULL));
	Deck* d = new Deck();
	d->createDeck();
	
	for(int i=0;i<5;i++)
	{
		cout << d->Cards[i]->GetCardName()<<"\n";
	}
	return 0;
}
