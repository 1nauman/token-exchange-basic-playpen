package main

import (
	"context"
	"crypto/rsa"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"os"
	"strings"
	"sync"
	"time"

	"github.com/golang-jwt/jwt/v5"
)

const (
	issuer = "my-api-gateway"
)

var rsaPublicKey *rsa.PublicKey

// Product represents the structure of a product
type Product struct {
	ID          int     `json:"id"`
	Name        string  `json:"name"`
	Description string  `json:"description"`
	Price       float64 `json:"price"`
}

// InventoryItem represents the structure of an inventory item
type InventoryItem struct {
	ProductID  int `json:"productId"`
	StockCount int `json:"stockCount"`
}

// ProductDetail represents the aggregated product details
type ProductDetail struct {
	ID          int     `json:"id"`
	Name        string  `json:"name"`
	Description string  `json:"description"`
	Price       float64 `json:"price"`
	StockCount  int     `json:"stockCount"`
}

func main() {
	// Load the public key
	publicKeyPEM, err := os.ReadFile("/app/public_key.pem")
	if err != nil {
		log.Fatalf("failed to read public key: %v", err)
	}
	rsaPublicKey, err = jwt.ParseRSAPublicKeyFromPEM(publicKeyPEM)
	if err != nil {
		log.Fatalf("failed to parse public key: %v", err)
	}

	http.Handle("/api-go/products/", jwtMiddleware(http.HandlerFunc(productsHandler)))

	log.Println("Go BFF API is running on port 8081")
	if err := http.ListenAndServe(":8081", nil); err != nil {
		log.Fatalf("could not listen on port 8081 %v", err)
	}
}

func jwtMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		authHeader := r.Header.Get("Authorization")
		if authHeader == "" {
			http.Error(w, "Authorization header required", http.StatusUnauthorized)
			return
		}

		tokenString := strings.TrimPrefix(authHeader, "Bearer ")
		if tokenString == authHeader {
			http.Error(w, "Bearer token required", http.StatusUnauthorized)
			return
		}

		token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
			if _, ok := token.Method.(*jwt.SigningMethodRSA); !ok {
				return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
			}
			return rsaPublicKey, nil
		}, jwt.WithIssuer(issuer))

		if err != nil {
			http.Error(w, fmt.Sprintf("Invalid token: %v", err), http.StatusUnauthorized)
			return
		}

		if !token.Valid {
			http.Error(w, "Invalid token", http.StatusUnauthorized)
			return
		}

		ctx := context.WithValue(r.Context(), "user", token.Claims.(jwt.MapClaims))
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

func productsHandler(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimPrefix(r.URL.Path, "/api-go/products/")
	if id == "" {
		http.Error(w, "Product ID required", http.StatusBadRequest)
		return
	}

	userClaims := r.Context().Value("user").(jwt.MapClaims)
	userName := userClaims["preferred_username"].(string)
	log.Printf("---> Go BFF: Aggregation started for product %s, user: %s", id, userName)

	var wg sync.WaitGroup
	var product Product
	var inventory InventoryItem
	var productErr, inventoryErr error

	client := &http.Client{Timeout: 5 * time.Second}

	// Make parallel calls to the Product and Inventory APIs
	wg.Add(2)

	go func() {
		defer wg.Done()
		product, productErr = getProduct(client, id, r.Header.Get("Authorization"))
	}()

	go func() {
		defer wg.Done()
		inventory, inventoryErr = getInventory(client, id)
	}()

	wg.Wait()

	if productErr != nil {
		http.Error(w, fmt.Sprintf("Product API returned an error: %v", productErr), http.StatusBadGateway)
		return
	}

	stockCount := 0
	if inventoryErr != nil {
		log.Printf("---> Go BFF: Warning - Inventory API call failed: %v. Defaulting stock to 0.", inventoryErr)
	} else {
		stockCount = inventory.StockCount
	}

	productDetail := ProductDetail{
		ID:          product.ID,
		Name:        product.Name,
		Description: product.Description,
		Price:       product.Price,
		StockCount:  stockCount,
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(productDetail)
	log.Printf("---> Go BFF: Aggregation complete for product %s.", id)
}

func getProduct(client *http.Client, id, authHeader string) (Product, error) {
	var product Product
	req, err := http.NewRequest("GET", fmt.Sprintf("http://product-api:8080/api/products/%s", id), nil)
	if err != nil {
		return product, err
	}
	req.Header.Set("Authorization", authHeader)

	resp, err := client.Do(req)
	if err != nil {
		return product, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return product, fmt.Errorf("unexpected status code: %d, body: %s", resp.StatusCode, string(body))
	}

	if err := json.NewDecoder(resp.Body).Decode(&product); err != nil {
		return product, err
	}
	return product, nil
}

func getInventory(client *http.Client, id string) (InventoryItem, error) {
	var inventory InventoryItem
	resp, err := client.Get(fmt.Sprintf("http://inventory-api:8080/inventory/%s", id))
	if err != nil {
		return inventory, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		return inventory, fmt.Errorf("unexpected status code: %d, body: %s", resp.StatusCode, string(body))
	}

	if err := json.NewDecoder(resp.Body).Decode(&inventory); err != nil {
		return inventory, err
	}
	return inventory, nil
}
